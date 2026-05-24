using System.ComponentModel.DataAnnotations;

namespace Koot.Api.Models;

public class GameAnswer
{
    public int Id { get; set; }

    public int SessionId { get; set; }
    public GameSession? Session { get; set; }

    public int ParticipantId { get; set; }
    public GameParticipant? Participant { get; set; }

    public int QuestionId { get; set; }
    public Question? Question { get; set; }

    /// <summary>Selected option for choice-style questions.</summary>
    public int? AnswerOptionId { get; set; }
    public AnswerOption? AnswerOption { get; set; }

    /// <summary>Free-text answer for TypeAnswer questions.</summary>
    [MaxLength(500)]
    public string? AnswerText { get; set; }

    public int TimeTakenMs { get; set; }

    public int PointsEarned { get; set; }

    public bool IsCorrect { get; set; }

    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;
}
