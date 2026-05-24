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
}
