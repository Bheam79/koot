using System.Collections.Concurrent;
using Koot.Api.Data;
using Koot.Api.Dtos.Games;
using Koot.Api.Hubs;
using Koot.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Koot.Api.Services;

// ── In-memory representation of a single question's metadata ────────────────

public class ActiveQuestionInfo
{
    public int Id { get; set; }
    public int TimeLimit { get; set; }
    public int Points { get; set; }
    public QuestionType Type { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int OrderIndex { get; set; }
    public List<ActiveOptionInfo> AnswerOptions { get; set; } = new();
}

public class ActiveOptionInfo
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int OrderIndex { get; set; }
}

// ── In-memory representation of an active game session ──────────────────────

public class ActiveSession
{
    public int DbId { get; set; }
    public string Code { get; set; } = string.Empty;
    public int HostUserId { get; set; }
    public GameStatus Status { get; set; } = GameStatus.Lobby;
    public int CurrentQuestionIndex { get; set; } = -1;
    public List<ActiveQuestionInfo> Questions { get; set; } = new();

    /// <summary>SignalR connectionId → participantId</summary>
    public ConcurrentDictionary<string, int> ConnectionToParticipant { get; } = new();

    /// <summary>participantId → SignalR connectionId (null if disconnected)</summary>
    public ConcurrentDictionary<int, string?> ParticipantToConnection { get; } = new();

    /// <summary>participantIds that have answered the current question.</summary>
    public HashSet<int> AnsweredThisQuestion { get; set; } = new();

    /// <summary>participantIds that are currently disconnected.</summary>
    public HashSet<int> DisconnectedParticipants { get; set; } = new();

    public CancellationTokenSource? QuestionTimerCts { get; set; }

    /// <summary>Per-session lock guarding state transitions.</summary>
    public SemaphoreSlim Lock { get; } = new(1, 1);

    public int ActivePlayerCount =>
        ParticipantToConnection.Count(kvp => !DisconnectedParticipants.Contains(kvp.Key));
}

// ── Service ──────────────────────────────────────────────────────────────────

/// <summary>
/// Singleton that holds all in-memory game state and manages server-side timers.
/// Hub methods call into this service; timer callbacks broadcast via IHubContext.
/// </summary>
public class GameStateService
{
    private readonly ConcurrentDictionary<string, ActiveSession> _sessions = new();
    private readonly IHubContext<GameHub> _hub;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GameStateService> _logger;

    public GameStateService(
        IHubContext<GameHub> hub,
        IServiceScopeFactory scopeFactory,
        ILogger<GameStateService> logger)
    {
        _hub = hub;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // ── Session lifecycle ────────────────────────────────────────────────────

    public ActiveSession CreateSession(GameSession dbSession, List<ActiveQuestionInfo> questions)
    {
        var s = new ActiveSession
        {
            DbId = dbSession.Id,
            Code = dbSession.Code,
            HostUserId = dbSession.HostUserId,
            Status = dbSession.Status,
            Questions = questions,
        };
        _sessions[dbSession.Code] = s;
        return s;
    }

    public ActiveSession? GetSession(string code) =>
        _sessions.TryGetValue(code, out var s) ? s : null;

    public void RemoveSession(string code) => _sessions.TryRemove(code, out _);

    // ── Connection tracking ──────────────────────────────────────────────────

    public void TrackPlayerConnection(string code, string connectionId, int participantId)
    {
        if (!_sessions.TryGetValue(code, out var s)) return;
        s.ConnectionToParticipant[connectionId] = participantId;
        s.ParticipantToConnection[participantId] = connectionId;
        s.DisconnectedParticipants.Remove(participantId);
    }

    /// <summary>
    /// Called when a SignalR connection drops.
    /// Returns (code, participantId) if it was a tracked player, else null.
    /// </summary>
    public (string code, int participantId)? HandleDisconnect(string connectionId)
    {
        foreach (var (code, session) in _sessions)
        {
            if (session.ConnectionToParticipant.TryRemove(connectionId, out var participantId))
            {
                session.ParticipantToConnection.TryUpdate(participantId, null!, connectionId);
                session.DisconnectedParticipants.Add(participantId);
                return (code, participantId);
            }
        }
        return null;
    }

    public int? GetParticipantIdByConnection(string connectionId)
    {
        foreach (var session in _sessions.Values)
        {
            if (session.ConnectionToParticipant.TryGetValue(connectionId, out var pid))
                return pid;
        }
        return null;
    }

    public ActiveSession? GetSessionByConnection(string connectionId)
    {
        foreach (var session in _sessions.Values)
        {
            if (session.ConnectionToParticipant.ContainsKey(connectionId))
                return session;
        }
        return null;
    }

    // ── Answer tracking ──────────────────────────────────────────────────────

    /// <summary>
    /// Records that a participant answered.  Returns true if all active players
    /// have now answered (caller should end the question early).
    /// </summary>
    public bool RecordAnswer(string code, int participantId)
    {
        if (!_sessions.TryGetValue(code, out var s)) return false;
        s.AnsweredThisQuestion.Add(participantId);
        return s.AnsweredThisQuestion.Count >= s.ActivePlayerCount && s.ActivePlayerCount > 0;
    }

    // ── Question flow ────────────────────────────────────────────────────────

    /// <summary>
    /// Starts broadcasting a question and its countdown.  When the timer expires
    /// (or is cancelled early) QuestionEnded/LeaderboardUpdate are sent, then
    /// either the next question is awaited or GameEnded is sent.
    /// </summary>
    public async Task BroadcastQuestionAsync(string code)
    {
        if (!_sessions.TryGetValue(code, out var session)) return;

        var q = session.Questions[session.CurrentQuestionIndex];
        var broadcast = BuildBroadcast(q);

        // Reset per-question answer tracking
        session.AnsweredThisQuestion = new HashSet<int>();

        // Cancel any running timer
        session.QuestionTimerCts?.Cancel();
        var cts = new CancellationTokenSource();
        session.QuestionTimerCts = cts;

        // Broadcast question to all
        await _hub.Clients.Group($"game-{code}")
            .SendAsync("QuestionStarted", broadcast, q.TimeLimit, cts.Token);

        // Run the countdown in the background
        _ = RunTimerAsync(code, session, q, cts.Token);
    }

    private async Task RunTimerAsync(
        string code, ActiveSession session, ActiveQuestionInfo q,
        CancellationToken ct)
    {
        try
        {
            for (int s = q.TimeLimit; s >= 0; s--)
            {
                if (ct.IsCancellationRequested) return;
                await _hub.Clients.Group($"game-{code}").SendAsync("TimerTick", s, ct);
                if (s == 0) break;
                await Task.Delay(1000, ct);
            }

            if (!ct.IsCancellationRequested)
                await EndQuestionAsync(code);
        }
        catch (OperationCanceledException)
        {
            // Timer was cancelled (early end or next question) — no action needed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Timer error for session {Code}", code);
        }
    }

    /// <summary>
    /// Ends the current question: cancels timer, broadcasts results and leaderboard,
    /// then decides whether to wait for host (NextQuestion / EndGame) or auto-advance.
    /// </summary>
    public async Task EndQuestionAsync(string code)
    {
        if (!_sessions.TryGetValue(code, out var session)) return;

        await session.Lock.WaitAsync();
        try
        {
            // Cancel timer if still running
            session.QuestionTimerCts?.Cancel();
            session.QuestionTimerCts = null;

            var q = session.Questions[session.CurrentQuestionIndex];

            // Load DB data for results
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var dbSession = await db.GameSessions
                .Include(s => s.Participants)
                .FirstOrDefaultAsync(s => s.Code == code);

            if (dbSession is null) return;

            var answers = await db.GameAnswers
                .Where(a => a.SessionId == dbSession.Id && a.QuestionId == q.Id)
                .ToListAsync();

            // Build result set (participants without an answer get 0)
            var results = dbSession.Participants
                .OrderBy(p => p.Id)
                .Select(p =>
                {
                    var ans = answers.FirstOrDefault(a => a.ParticipantId == p.Id);
                    return new AnswerResultDto
                    {
                        ParticipantId = p.Id,
                        Nickname = p.Nickname,
                        PointsEarned = ans?.PointsEarned ?? 0,
                        TotalScore = p.TotalScore,
                        IsCorrect = ans?.IsCorrect ?? false,
                    };
                })
                .ToList();

            // Correct answer info for broadcast
            var correctAnswer = q.AnswerOptions
                .Where(o => o.IsCorrect)
                .Select(o => new { o.Id, o.Text })
                .ToList();

            await _hub.Clients.Group($"game-{code}")
                .SendAsync("QuestionEnded", correctAnswer, results);

            // Build top-10 leaderboard
            var leaderboard = dbSession.Participants
                .OrderByDescending(p => p.TotalScore)
                .Take(10)
                .Select((p, i) => new LeaderboardEntryDto
                {
                    Rank = i + 1,
                    ParticipantId = p.Id,
                    Nickname = p.Nickname,
                    AvatarId = p.AvatarId,
                    TotalScore = p.TotalScore,
                })
                .ToList();

            await _hub.Clients.Group($"game-{code}")
                .SendAsync("LeaderboardUpdate", leaderboard);
        }
        finally
        {
            session.Lock.Release();
        }
    }

    /// <summary>Finalises the game: marks DB finished, broadcasts final standings.</summary>
    public async Task EndGameAsync(string code)
    {
        if (!_sessions.TryGetValue(code, out var session)) return;

        session.QuestionTimerCts?.Cancel();
        session.Status = GameStatus.Finished;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var dbSession = await db.GameSessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Code == code);

        if (dbSession is not null)
        {
            dbSession.Status = GameStatus.Finished;
            dbSession.EndedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var finalStandings = dbSession.Participants
                .OrderByDescending(p => p.TotalScore)
                .Select((p, i) => new LeaderboardEntryDto
                {
                    Rank = i + 1,
                    ParticipantId = p.Id,
                    Nickname = p.Nickname,
                    AvatarId = p.AvatarId,
                    TotalScore = p.TotalScore,
                })
                .ToList();

            await _hub.Clients.Group($"game-{code}").SendAsync("GameEnded", finalStandings);
        }

        RemoveSession(code);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static QuestionBroadcastDto BuildBroadcast(ActiveQuestionInfo q) => new()
    {
        Id = q.Id,
        Type = (int)q.Type,
        QuestionText = q.QuestionText,
        TimeLimit = q.TimeLimit,
        Points = q.Points,
        ImageUrl = q.ImageUrl,
        OrderIndex = q.OrderIndex,
        AnswerOptions = q.AnswerOptions
            .OrderBy(o => o.OrderIndex)
            .Select(o => new AnswerOptionBroadcastDto { Id = o.Id, Text = o.Text, OrderIndex = o.OrderIndex })
            .ToList(),
    };
}
