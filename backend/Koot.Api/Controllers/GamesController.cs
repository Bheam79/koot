using Koot.Api.Data;
using Koot.Api.Dtos.Games;
using Koot.Api.Models;
using Koot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Koot.Api.Controllers;

[ApiController]
[Route("api/games")]
public class GamesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly GameService _gameService;
    private readonly GameStateService _stateService;
    private readonly ILogger<GamesController> _logger;

    public GamesController(
        AppDbContext db,
        GameService gameService,
        GameStateService stateService,
        ILogger<GamesController> logger)
    {
        _db = db;
        _gameService = gameService;
        _stateService = stateService;
        _logger = logger;
    }

    // ── POST /api/games/start ────────────────────────────────────────────────

    /// <summary>Start a new game session for the given quiz.</summary>
    [HttpPost("start")]
    [Authorize]
    public async Task<ActionResult<StartGameResponse>> Start([FromBody] StartGameRequest request)
    {
        if (!CurrentUser.TryGetId(User, out var userId))
            return Unauthorized();

        var quiz = await _db.Quizzes
            .Include(q => q.Questions.OrderBy(qu => qu.OrderIndex))
                .ThenInclude(qu => qu.AnswerOptions.OrderBy(o => o.OrderIndex))
            .FirstOrDefaultAsync(q => q.Id == request.QuizId);

        if (quiz is null)
            return NotFound("Quiz not found.");

        if (quiz.UserId != userId)
            return Forbid();

        if (!quiz.Questions.Any())
            return BadRequest("Quiz has no questions.");

        var code = await _gameService.GenerateUniqueCodeAsync();

        var session = new GameSession
        {
            QuizId = quiz.Id,
            HostUserId = userId,
            Code = code,
            Status = GameStatus.Lobby,
            CurrentQuestionIndex = -1,
            CreatedAt = DateTime.UtcNow,
        };

        _db.GameSessions.Add(session);
        await _db.SaveChangesAsync();

        // Build in-memory state so the hub can start immediately
        var questionInfos = quiz.Questions
            .OrderBy(q => q.OrderIndex)
            .Select(q => new ActiveQuestionInfo
            {
                Id = q.Id,
                Type = q.Type,
                QuestionText = q.QuestionText,
                TimeLimit = q.TimeLimit,
                Points = q.Points,
                ImageUrl = q.ImageUrl,
                OrderIndex = q.OrderIndex,
                AnswerOptions = q.AnswerOptions
                    .OrderBy(o => o.OrderIndex)
                    .Select(o => new ActiveOptionInfo
                    {
                        Id = o.Id,
                        Text = o.Text,
                        IsCorrect = o.IsCorrect,
                        OrderIndex = o.OrderIndex,
                    })
                    .ToList(),
            })
            .ToList();

        _stateService.CreateSession(session, questionInfos);

        _logger.LogInformation(
            "Game session {Code} (id {Id}) created by user {UserId} for quiz {QuizId}",
            code, session.Id, userId, quiz.Id);

        var joinUrl = $"{Request.Scheme}://{Request.Host}/join/{code}";

        return CreatedAtAction(nameof(GetInfo), new { code }, new StartGameResponse
        {
            Id = session.Id,
            Code = code,
            JoinUrl = joinUrl,
        });
    }

    // ── GET /api/games/{code} ────────────────────────────────────────────────

    /// <summary>Returns session metadata (public — used by players joining).</summary>
    [HttpGet("{code}")]
    public async Task<ActionResult<SessionInfoDto>> GetInfo(string code)
    {
        var session = await _db.GameSessions
            .Include(s => s.Quiz)
            .Include(s => s.HostUser)
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Code == code.ToUpperInvariant());

        if (session is null)
            return NotFound();

        return Ok(new SessionInfoDto
        {
            Id = session.Id,
            Code = session.Code,
            QuizTitle = session.Quiz!.Title,
            HostName = session.HostUser!.Username,
            Status = session.Status.ToString(),
            ParticipantCount = session.Participants.Count,
        });
    }

    // ── GET /api/games/{code}/participants ───────────────────────────────────

    /// <summary>Returns the participant list for a session.</summary>
    [HttpGet("{code}/participants")]
    public async Task<ActionResult<IEnumerable<ParticipantDto>>> GetParticipants(string code)
    {
        var session = await _db.GameSessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Code == code.ToUpperInvariant());

        if (session is null)
            return NotFound();

        var dtos = session.Participants
            .OrderBy(p => p.JoinedAt)
            .Select(p => new ParticipantDto
            {
                Id = p.Id,
                Nickname = p.Nickname,
                AvatarId = p.AvatarId,
                TotalScore = p.TotalScore,
                IsDisconnected = p.IsDisconnected,
                JoinedAt = p.JoinedAt,
            });

        return Ok(dtos);
    }

    // ── DELETE /api/games/{code} ─────────────────────────────────────────────

    /// <summary>End / cancel a session (host only).</summary>
    [HttpDelete("{code}")]
    [Authorize]
    public async Task<IActionResult> Cancel(string code)
    {
        if (!CurrentUser.TryGetId(User, out var userId))
            return Unauthorized();

        var session = await _db.GameSessions
            .FirstOrDefaultAsync(s => s.Code == code.ToUpperInvariant());

        if (session is null)
            return NotFound();

        if (session.HostUserId != userId)
            return Forbid();

        if (session.Status == GameStatus.Finished)
            return BadRequest("Session is already finished.");

        session.Status = GameStatus.Finished;
        session.EndedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Also end in-memory state
        await _stateService.EndGameAsync(code.ToUpperInvariant());

        _logger.LogInformation("Session {Code} cancelled by host {UserId}", code, userId);
        return NoContent();
    }

    // ── GET /api/games/history ───────────────────────────────────────────────

    /// <summary>
    /// Paginated history of Finished sessions hosted by the current user. Includes
    /// per-session participant count, average score and top scorer so the dashboard
    /// can render the list without a round-trip per row.
    /// </summary>
    [HttpGet("history")]
    [Authorize]
    public async Task<ActionResult<PagedResultDto<GameHistorySummaryDto>>> GetHistory(
        [FromQuery] int? quizId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!CurrentUser.TryGetId(User, out var userId))
            return Unauthorized();

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var baseQuery = _db.GameSessions
            .AsNoTracking()
            .Where(s => s.HostUserId == userId && s.Status == GameStatus.Finished);

        if (quizId.HasValue)
            baseQuery = baseQuery.Where(s => s.QuizId == quizId.Value);

        var total = await baseQuery.CountAsync();

        // Page of session metadata + quiz title, ordered newest-finished first.
        var sessionRows = await baseQuery
            .OrderByDescending(s => s.EndedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.Code,
                s.QuizId,
                QuizTitle = s.Quiz!.Title,
                s.StartedAt,
                s.EndedAt,
            })
            .ToListAsync();

        if (sessionRows.Count == 0)
        {
            return Ok(new PagedResultDto<GameHistorySummaryDto>
            {
                Items = Array.Empty<GameHistorySummaryDto>(),
                Total = total,
                Page = page,
                PageSize = pageSize,
            });
        }

        var sessionIds = sessionRows.Select(s => s.Id).ToList();

        // One round-trip for all participants across the page; aggregation is
        // done in memory because participants-per-session is small.
        var participants = await _db.GameParticipants
            .AsNoTracking()
            .Where(p => sessionIds.Contains(p.SessionId))
            .Select(p => new { p.SessionId, p.Nickname, p.TotalScore })
            .ToListAsync();

        var participantsBySession = participants
            .GroupBy(p => p.SessionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var items = sessionRows.Select(s =>
        {
            var summary = new GameHistorySummaryDto
            {
                Id = s.Id,
                Code = s.Code,
                QuizId = s.QuizId,
                QuizTitle = s.QuizTitle,
                StartedAt = s.StartedAt,
                EndedAt = s.EndedAt,
                DurationSeconds = (s.StartedAt.HasValue && s.EndedAt.HasValue)
                    ? (int)Math.Max(0, (s.EndedAt.Value - s.StartedAt.Value).TotalSeconds)
                    : (int?)null,
            };

            if (participantsBySession.TryGetValue(s.Id, out var ps) && ps.Count > 0)
            {
                summary.ParticipantCount = ps.Count;
                summary.AverageScore = ps.Average(x => (double)x.TotalScore);
                var top = ps.OrderByDescending(x => x.TotalScore).First();
                summary.TopScorerNickname = top.Nickname;
                summary.TopScorerScore = top.TotalScore;
            }

            return summary;
        }).ToList();

        return Ok(new PagedResultDto<GameHistorySummaryDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize,
        });
    }

    // ── GET /api/games/sessions/{id} ─────────────────────────────────────────

    /// <summary>
    /// Detailed analytics for a single hosted session: standings (with correct
    /// counts) and per-question stats (accuracy, timing, option distribution).
    /// 404 if the session doesn't exist; 403 if it's owned by another user.
    /// </summary>
    [HttpGet("sessions/{id:int}")]
    [Authorize]
    public async Task<ActionResult<SessionDetailDto>> GetSessionDetail(int id)
    {
        if (!CurrentUser.TryGetId(User, out var userId))
            return Unauthorized();

        var session = await _db.GameSessions
            .AsNoTracking()
            .Include(s => s.Quiz)
                .ThenInclude(q => q!.Questions.OrderBy(qu => qu.OrderIndex))
                    .ThenInclude(qu => qu.AnswerOptions.OrderBy(o => o.OrderIndex))
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session is null)
            return NotFound();

        if (session.HostUserId != userId)
            return Forbid();

        var participants = await _db.GameParticipants
            .AsNoTracking()
            .Where(p => p.SessionId == id)
            .ToListAsync();

        var answers = await _db.GameAnswers
            .AsNoTracking()
            .Where(a => a.SessionId == id)
            .ToListAsync();

        var quizQuestions = session.Quiz?.Questions
            .OrderBy(q => q.OrderIndex)
            .ToList() ?? new List<Models.Question>();
        var totalQuestions = quizQuestions.Count;

        // ── Standings ───────────────────────────────────────────────────────
        var correctByParticipant = answers
            .Where(a => a.IsCorrect)
            .GroupBy(a => a.ParticipantId)
            .ToDictionary(g => g.Key, g => g.Count());

        var standings = participants
            .OrderByDescending(p => p.TotalScore)
            .ThenBy(p => p.Nickname)
            .Select((p, idx) => new SessionStandingDto
            {
                Rank = idx + 1,
                Nickname = p.Nickname,
                AvatarId = p.AvatarId,
                TotalScore = p.TotalScore,
                CorrectCount = correctByParticipant.TryGetValue(p.Id, out var c) ? c : 0,
                TotalQuestions = totalQuestions,
            })
            .ToList();

        // ── Per-question stats ──────────────────────────────────────────────
        var answersByQuestion = answers
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var questionStats = quizQuestions.Select(q =>
        {
            answersByQuestion.TryGetValue(q.Id, out var qAnswers);
            qAnswers ??= new List<Models.GameAnswer>();

            var stat = new QuestionStatDto
            {
                QuestionId = q.Id,
                QuestionText = q.QuestionText,
                QuestionType = q.Type,
                OrderIndex = q.OrderIndex,
                AnswerCount = qAnswers.Count,
                CorrectCount = qAnswers.Count(a => a.IsCorrect),
            };

            stat.AccuracyPct = stat.AnswerCount > 0
                ? Math.Round(100.0 * stat.CorrectCount / stat.AnswerCount, 2)
                : 0;

            if (qAnswers.Count > 0)
            {
                stat.AvgTimeTakenMs = Math.Round(qAnswers.Average(a => (double)a.TimeTakenMs), 2);
                stat.MedianTimeTakenMs = Math.Round(Median(qAnswers.Select(a => a.TimeTakenMs)), 2);
            }

            if (q.Type == QuestionType.TypeAnswer)
            {
                stat.CorrectAnswerTexts = q.AnswerOptions
                    .Where(o => o.IsCorrect)
                    .Select(o => o.Text)
                    .ToList();
            }
            else
            {
                // MultipleChoice / TrueFalse / Poll: distribution across the options.
                var pickedByOption = qAnswers
                    .Where(a => a.AnswerOptionId.HasValue)
                    .GroupBy(a => a.AnswerOptionId!.Value)
                    .ToDictionary(g => g.Key, g => g.Count());

                stat.OptionDistribution = q.AnswerOptions
                    .OrderBy(o => o.OrderIndex)
                    .Select(o =>
                    {
                        var pick = pickedByOption.TryGetValue(o.Id, out var c) ? c : 0;
                        return new OptionDistributionDto
                        {
                            OptionId = o.Id,
                            OptionText = o.Text,
                            PickCount = pick,
                            PickPct = stat.AnswerCount > 0
                                ? Math.Round(100.0 * pick / stat.AnswerCount, 2)
                                : 0,
                        };
                    })
                    .ToList();
            }

            return stat;
        }).ToList();

        return Ok(new SessionDetailDto
        {
            Id = session.Id,
            Code = session.Code,
            QuizId = session.QuizId,
            QuizTitle = session.Quiz?.Title ?? string.Empty,
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt,
            DurationSeconds = (session.StartedAt.HasValue && session.EndedAt.HasValue)
                ? (int)Math.Max(0, (session.EndedAt.Value - session.StartedAt.Value).TotalSeconds)
                : (int?)null,
            Standings = standings,
            QuestionStats = questionStats,
        });
    }

    // ── GET /api/games/sessions/{id}/answers ─────────────────────────────────

    /// <summary>
    /// Flat list of every submitted answer in the session, shaped for CSV
    /// export. No pagination — hosts download all rows at once.
    /// </summary>
    [HttpGet("sessions/{id:int}/answers")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<SessionAnswerRowDto>>> GetSessionAnswers(int id)
    {
        if (!CurrentUser.TryGetId(User, out var userId))
            return Unauthorized();

        var session = await _db.GameSessions
            .AsNoTracking()
            .Select(s => new { s.Id, s.HostUserId })
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session is null)
            return NotFound();

        if (session.HostUserId != userId)
            return Forbid();

        var rows = await _db.GameAnswers
            .AsNoTracking()
            .Where(a => a.SessionId == id)
            .OrderBy(a => a.Question!.OrderIndex)
            .ThenBy(a => a.AnsweredAt)
            .Select(a => new SessionAnswerRowDto
            {
                ParticipantNickname = a.Participant!.Nickname,
                QuestionText = a.Question!.QuestionText,
                QuestionType = a.Question!.Type,
                AnswerText = a.AnswerText,
                SelectedOptionText = a.AnswerOption != null ? a.AnswerOption.Text : null,
                IsCorrect = a.IsCorrect,
                PointsEarned = a.PointsEarned,
                TimeTakenMs = a.TimeTakenMs,
                AnsweredAt = a.AnsweredAt,
            })
            .ToListAsync();

        return Ok(rows);
    }

    private static double Median(IEnumerable<int> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var n = sorted.Count;
        if (n == 0) return 0;
        if (n % 2 == 1) return sorted[n / 2];
        return (sorted[(n / 2) - 1] + sorted[n / 2]) / 2.0;
    }
}
