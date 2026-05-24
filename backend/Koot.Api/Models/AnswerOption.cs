using System.ComponentModel.DataAnnotations;

namespace Koot.Api.Models;

public class AnswerOption
{
    public int Id { get; set; }

    public int QuestionId { get; set; }
    public Question? Question { get; set; }

    [Required, MaxLength(500)]
    public string Text { get; set; } = string.Empty;

    public bool IsCorrect { get; set; }

    public int OrderIndex { get; set; }

    // Navigation
    public ICollection<GameAnswer> GameAnswers { get; set; } = new List<GameAnswer>();
}
