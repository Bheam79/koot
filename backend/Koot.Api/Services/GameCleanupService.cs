using Koot.Api.Data;
using Koot.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Koot.Api.Services;

/// <summary>
/// Hosted background service that periodically marks stale sessions as Finished
/// and removes their in-memory state.  Runs every 30 minutes; targets sessions
/// created more than 24 hours ago that are still in Lobby or InProgress.
/// </summary>
public class GameCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan SessionMaxAge = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GameStateService _stateService;
    private readonly ILogger<GameCleanupService> _logger;

    public GameCleanupService(
        IServiceScopeFactory scopeFactory,
        GameStateService stateService,
        ILogger<GameCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _stateService = stateService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GameCleanupService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken);

            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during game session cleanup.");
            }
        }
    }

    /// <summary>
    /// Marks Lobby/InProgress sessions older than <see cref="SessionMaxAge"/>
    /// as Finished and drops their in-memory state.
    /// <para>
    /// Finished sessions are intentionally never deleted; they feed the
    /// history/analytics API.
    /// </para>
    /// </summary>
    private async Task CleanupAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTime.UtcNow - SessionMaxAge;

        var staleSessions = await db.GameSessions
            .Where(s => s.Status != GameStatus.Finished && s.CreatedAt < cutoff)
            .ToListAsync(ct);

        if (staleSessions.Count == 0) return;

        foreach (var session in staleSessions)
        {
            session.Status = GameStatus.Finished;
            session.EndedAt = DateTime.UtcNow;
            _stateService.RemoveSession(session.Code);
        }

        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Cleaned up {Count} stale game session(s) older than {Age}.",
            staleSessions.Count, SessionMaxAge);
    }
}
