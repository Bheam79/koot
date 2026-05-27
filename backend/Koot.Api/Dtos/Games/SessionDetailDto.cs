using Koot.Api.Models;

namespace Koot.Api.Dtos.Games;

/// <summary>
/// Full analytics payload for a single (Finished) game session.
/// Hosts use this to view standings + per-question breakdown.
/// </summary>
public class SessionDetailDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;

    public int QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;

    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int? DurationSeconds { get; set; }

    public IReadOnlyList<SessionStandingDto> Standings { get; set; } = Array.Empty<SessionStandingDto>();
    public IReadOnlyList<QuestionStatDto> QuestionStats { get; set; } = Array.Empty<QuestionStatDto>();
}

public class SessionStandingDto
{
    public int Rank { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public int AvatarId { get; set; }
    public int TotalScore { get; set; }
    public int CorrectCount { get; set; }
    public int TotalQuestions { get; set; }
}

public class QuestionStatDto
{
    public int QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public QuestionType QuestionType { get; set; }
    public int OrderIndex { get; set; }

    public int AnswerCount { get; set; }
    public int CorrectCount { get; set; }

    /// <summary>0..100 — share of submitted answers that were correct.</summary>
    public double AccuracyPct { get; set; }

    public double AvgTimeTakenMs { get; set; }
    public double MedianTimeTakenMs { get; set; }

    /// <summary>For MultipleChoice / TrueFalse / Poll: distribution across the options.</summary>
    public IReadOnlyList<OptionDistributionDto> OptionDistribution { get; set; } = Array.Empty<OptionDistributionDto>();

    /// <summary>For TypeAnswer: the literal correct text(s) configured on the question.</summary>
    public IReadOnlyList<string> CorrectAnswerTexts { get; set; } = Array.Empty<string>();
}

public class OptionDistributionDto
{
    public int OptionId { get; set; }
    public string OptionText { get; set; } = string.Empty;
    public int PickCount { get; set; }

    /// <summary>0..100 — share of submitted answers picking this option.</summary>
    public double PickPct { get; set; }
}
