namespace Koot.Api.Dtos.Games;

/// <summary>
/// One row in the host's game-history list. Aggregates per-session metrics
/// (participant count, average score, top scorer) so the dashboard doesn't
/// need to round-trip per session.
/// </summary>
public class GameHistorySummaryDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;

    public int QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;

    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    /// <summary>
    /// Duration of the played session in seconds, computed from
    /// <see cref="StartedAt"/> and <see cref="EndedAt"/>. Null if either is unset.
    /// </summary>
    public int? DurationSeconds { get; set; }

    public int ParticipantCount { get; set; }
    public double AverageScore { get; set; }

    /// <summary>Nickname of the highest-scoring participant; null if no participants.</summary>
    public string? TopScorerNickname { get; set; }
    public int? TopScorerScore { get; set; }
}

/// <summary>Generic paginated envelope used for history responses.</summary>
public class PagedResultDto<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
