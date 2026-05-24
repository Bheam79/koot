using Koot.Api.Data;
using Koot.Api.Dtos.Games;
using Koot.Api.Models;
using Koot.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Koot.Api.Hubs;

/// <summary>
/// SignalR hub for real-time game sessions.
///
/// Groups:
///   host-{code}   — the session host
///   players-{code} — all joined players
///   game-{code}   — everyone (host + players), used for "broadcast all" events
/// </summary>
public class GameHub : Hub
{
    private readonly AppDbContext _db;
    private readonly GameStateService _stateService;
    private readonly ILogger<GameHub> _logger;

    public GameHub(AppDbContext db, GameStateService stateService, ILogger<GameHub> logger)
    {
        _db = db;
        _stateService = stateService;
        _logger = logger;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Disconnect
    // ═════════════════════════════════════════════════════════════════════════

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var result = _stateService.HandleDisconnect(Context.ConnectionId);
        if (result.HasValue)
        {
            var (code, participantId) = result.Value;

            // Persist disconnected flag
            var participant = await _db.GameParticipants.FindAsync(participantId);
            if (participant is not null)
            {
                participant.IsDisconnected = true;
                await _db.SaveChangesAsync();
            }

            await Clients.Group($"host-{code}")
                .SendAsync("PlayerLeft", participantId);

            _logger.LogInformation("Player {PId} disconnected from session {Code}", participantId, code);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Host methods
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Called by the host client to join the host group and receive the current session state.
    /// The caller must be authenticated and must be the host of the session.
    /// </summary>
    public async Task JoinAsHost(string code)
    {
        code = code.ToUpperInvariant();

        if (!TryGetUserId(out var userId))
        {
            await Clients.Caller.SendAsync("Error", "Authentication required.");
            return;
        }

        var session = await _db.GameSessions
            .Include(s => s.Quiz)
                .ThenInclude(q => q!.Questions)
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Code == code);

        if (session is null)
        {
            await Clients.Caller.SendAsync("Error", "Session not found.");
            return;
        }

        if (session.HostUserId != userId)
        {
            await Clients.Caller.SendAsync("Error", "You are not the host.");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"host-{code}");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"game-{code}");

        var stateDto = new
        {
            session.Id,
            session.Code,
            Status = session.Status.ToString(),
            session.CurrentQuestionIndex,
            QuizTitle = session.Quiz!.Title,
            TotalQuestions = session.Quiz.Questions.Count,
            Participants = session.Participants.Select(p => new ParticipantDto
            {
                Id = p.Id,
                Nickname = p.Nickname,
                AvatarId = p.AvatarId,
                TotalScore = p.TotalScore,
                IsDisconnected = p.IsDisconnected,
                JoinedAt = p.JoinedAt,
            }).ToList(),
        };

        await Clients.Caller.SendAsync("SessionState", stateDto);
        _logger.LogInformation("Host {UserId} joined session {Code}", userId, code);
    }

    /// <summary>Changes session status to InProgress and sends the first question.</summary>
    public async Task StartGame(string code)
    {
        code = code.ToUpperInvariant();

        if (!await AuthorizeHostAsync(code)) return;

        var activeSession = _stateService.GetSession(code);
        if (activeSession is null)
        {
            await Clients.Caller.SendAsync("Error", "Session state not found.");
            return;
        }

        if (activeSession.Status != GameStatus.Lobby)
        {
            await Clients.Caller.SendAsync("Error", "Game is not in Lobby status.");
            return;
        }

        if (!activeSession.Questions.Any())
        {
            await Clients.Caller.SendAsync("Error", "No questions in quiz.");
            return;
        }

        // Update DB
        var dbSession = await _db.GameSessions.FirstOrDefaultAsync(s => s.Code == code);
        if (dbSession is null) return;

        dbSession.Status = GameStatus.InProgress;
        dbSession.CurrentQuestionIndex = 0;
        dbSession.StartedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Update in-memory
        activeSession.Status = GameStatus.InProgress;
        activeSession.CurrentQuestionIndex = 0;

        // Notify players
        await Clients.Group($"players-{code}").SendAsync("GameStarted");

        // Broadcast first question (starts timer)
        await _stateService.BroadcastQuestionAsync(code);

        _logger.LogInformation("Game started for session {Code}", code);
    }

    /// <summary>Advances to the next question (host-driven).</summary>
    public async Task NextQuestion(string code)
    {
        code = code.ToUpperInvariant();

        if (!await AuthorizeHostAsync(code)) return;

        var activeSession = _stateService.GetSession(code);
        if (activeSession is null)
        {
            await Clients.Caller.SendAsync("Error", "Session state not found.");
            return;
        }

        if (activeSession.Status != GameStatus.InProgress)
        {
            await Clients.Caller.SendAsync("Error", "Game is not in progress.");
            return;
        }

        var nextIndex = activeSession.CurrentQuestionIndex + 1;
        if (nextIndex >= activeSession.Questions.Count)
        {
            await Clients.Caller.SendAsync("Error", "No more questions. Call EndGame.");
            return;
        }

        // Update DB + in-memory
        var dbSession = await _db.GameSessions.FirstOrDefaultAsync(s => s.Code == code);
        if (dbSession is null) return;

        dbSession.CurrentQuestionIndex = nextIndex;
        await _db.SaveChangesAsync();

        activeSession.CurrentQuestionIndex = nextIndex;

        await _stateService.BroadcastQuestionAsync(code);

        _logger.LogInformation("Question {Index} started for session {Code}", nextIndex, code);
    }

    /// <summary>Marks the session as Finished and broadcasts final standings.</summary>
    public async Task EndGame(string code)
    {
        code = code.ToUpperInvariant();

        if (!await AuthorizeHostAsync(code)) return;

        await _stateService.EndGameAsync(code);

        _logger.LogInformation("Game ended for session {Code}", code);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Player methods
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Adds the caller as a player in the session, notifies the host.
    /// Only allowed while the session is in Lobby status.
    /// </summary>
    public async Task JoinGame(string code, string nickname, int avatarId)
    {
        code = code.ToUpperInvariant();
        nickname = nickname.Trim();

        if (string.IsNullOrEmpty(nickname) || nickname.Length > 40)
        {
            await Clients.Caller.SendAsync("Error", "Nickname must be 1–40 characters.");
            return;
        }

        if (!Models.Avatars.IsValid(avatarId))
        {
            await Clients.Caller.SendAsync("Error", $"AvatarId must be {Models.Avatars.MinId}–{Models.Avatars.MaxId}.");
            return;
        }

        var session = await _db.GameSessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Code == code);

        if (session is null)
        {
            await Clients.Caller.SendAsync("Error", "Session not found.");
            return;
        }

        if (session.Status != GameStatus.Lobby)
        {
            await Clients.Caller.SendAsync("Error", "Game has already started.");
            return;
        }

        if (session.Participants.Any(p => p.Nickname.Equals(nickname, StringComparison.OrdinalIgnoreCase)))
        {
            await Clients.Caller.SendAsync("Error", "Nickname already taken.");
            return;
        }

        var participant = new GameParticipant
        {
            SessionId = session.Id,
            Nickname = nickname,
            AvatarId = avatarId,
            JoinedAt = DateTime.UtcNow,
        };

        _db.GameParticipants.Add(participant);
        await _db.SaveChangesAsync();

        // Track in-memory
        _stateService.TrackPlayerConnection(code, Context.ConnectionId, participant.Id);

        // Join SignalR groups
        await Groups.AddToGroupAsync(Context.ConnectionId, $"players-{code}");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"game-{code}");

        var dto = new ParticipantDto
        {
            Id = participant.Id,
            Nickname = participant.Nickname,
            AvatarId = participant.AvatarId,
            TotalScore = 0,
            IsDisconnected = false,
            JoinedAt = participant.JoinedAt,
        };

        // Notify host
        await Clients.Group($"host-{code}").SendAsync("PlayerJoined", dto);

        // Confirm to caller
        await Clients.Caller.SendAsync("JoinedGame", dto);

        _logger.LogInformation("Player '{Nickname}' (id {Id}) joined session {Code}", nickname, participant.Id, code);
    }

    /// <summary>
    /// Records a player's answer, calculates the score, and triggers early question
    /// end if all active players have now answered.
    /// </summary>
    public async Task SubmitAnswer(
        string code,
        int questionId,
        int? answerOptionId,
        string? answerText,
        int timeTakenMs)
    {
        code = code.ToUpperInvariant();

        var activeSession = _stateService.GetSession(code);
        if (activeSession is null)
        {
            await Clients.Caller.SendAsync("Error", "Session not found.");
            return;
        }

        if (activeSession.Status != GameStatus.InProgress)
        {
            await Clients.Caller.SendAsync("Error", "Game is not in progress.");
            return;
        }

        // Identify the player
        if (!activeSession.ConnectionToParticipant.TryGetValue(Context.ConnectionId, out var participantId))
        {
            await Clients.Caller.SendAsync("Error", "You are not a registered player.");
            return;
        }

        // Validate this is the current question
        var currentQ = activeSession.Questions[activeSession.CurrentQuestionIndex];
        if (currentQ.Id != questionId)
        {
            await Clients.Caller.SendAsync("Error", "Question ID does not match the current question.");
            return;
        }

        // Check for duplicate submission
        if (activeSession.AnsweredThisQuestion.Contains(participantId))
        {
            await Clients.Caller.SendAsync("Error", "Already answered this question.");
            return;
        }

        // Determine correctness
        bool isCorrect = DetermineCorrectness(currentQ, answerOptionId, answerText);
        int points = GameService.CalculateScore(isCorrect, timeTakenMs, currentQ.TimeLimit, currentQ.Points);

        // Persist answer
        var answer = new GameAnswer
        {
            SessionId = activeSession.DbId,
            ParticipantId = participantId,
            QuestionId = questionId,
            AnswerOptionId = answerOptionId,
            AnswerText = answerText?.Trim(),
            TimeTakenMs = Math.Max(0, timeTakenMs),
            PointsEarned = points,
            IsCorrect = isCorrect,
            AnsweredAt = DateTime.UtcNow,
        };

        _db.GameAnswers.Add(answer);

        // Update participant total score
        var participant = await _db.GameParticipants.FindAsync(participantId);
        if (participant is not null)
        {
            participant.TotalScore += points;
        }

        await _db.SaveChangesAsync();

        // Acknowledge to the caller
        await Clients.Caller.SendAsync("AnswerAccepted", new { points, isCorrect });

        // Track answer and check if everyone answered
        bool allAnswered = _stateService.RecordAnswer(code, participantId);
        if (allAnswered)
        {
            // End question early — all active players have answered
            _ = _stateService.EndQuestionAsync(code);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Private helpers
    // ═════════════════════════════════════════════════════════════════════════

    private bool TryGetUserId(out int userId)
    {
        userId = 0;
        return Koot.Api.Services.CurrentUser.TryGetId(Context.User!, out userId);
    }

    private async Task<bool> AuthorizeHostAsync(string code)
    {
        if (!TryGetUserId(out var userId))
        {
            await Clients.Caller.SendAsync("Error", "Authentication required.");
            return false;
        }

        var activeSession = _stateService.GetSession(code);
        if (activeSession is null)
        {
            // Fall back to DB check
            var dbSession = await _db.GameSessions.FirstOrDefaultAsync(s => s.Code == code);
            if (dbSession is null || dbSession.HostUserId != userId)
            {
                await Clients.Caller.SendAsync("Error", "Session not found or you are not the host.");
                return false;
            }
            return true;
        }

        if (activeSession.HostUserId != userId)
        {
            await Clients.Caller.SendAsync("Error", "You are not the host of this session.");
            return false;
        }

        return true;
    }

    private static bool DetermineCorrectness(
        ActiveQuestionInfo question,
        int? answerOptionId,
        string? answerText)
    {
        return question.Type switch
        {
            QuestionType.Poll => false, // polls are not scored
            QuestionType.TypeAnswer =>
                question.AnswerOptions.Any(o =>
                    o.IsCorrect &&
                    o.Text.Equals(answerText?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase)),
            _ => // MultipleChoice, TrueFalse
                answerOptionId.HasValue &&
                question.AnswerOptions.Any(o => o.Id == answerOptionId.Value && o.IsCorrect),
        };
    }
}
