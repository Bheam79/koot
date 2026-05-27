using Koot.Api.Models;

namespace Koot.Api.Dtos.Games;

/// <summary>
/// One flat row representing a single submitted answer in a session,
/// shaped for CSV export. Returned as a non-paginated list — hosts
/// download all answers at once.
/// </summary>
public class SessionAnswerRowDto
{
    public string ParticipantNickname { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public QuestionType QuestionType { get; set; }

    /// <summary>Free-text answer (TypeAnswer); null for choice-style questions.</summary>
    public string? AnswerText { get; set; }

    /// <summary>Text of the selected option (MC/TF/Poll); null for TypeAnswer.</summary>
    public string? SelectedOptionText { get; set; }

    public bool IsCorrect { get; set; }
    public int PointsEarned { get; set; }
    public int TimeTakenMs { get; set; }
    public DateTime AnsweredAt { get; set; }
}
