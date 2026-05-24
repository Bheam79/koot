using System.ComponentModel.DataAnnotations;

namespace Koot.Api.Models;

public enum QuestionType
{
    MultipleChoice = 0,
    TrueFalse = 1,
    TypeAnswer = 2,
    Poll = 3,
}

public class Question
{
    public int Id { get; set; }

    public int QuizId { get; set; }
    public Quiz? Quiz { get; set; }

    public int OrderIndex { get; set; }

    public QuestionType Type { get; set; } = QuestionType.MultipleChoice;

    [Required, MaxLength(1000)]
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>Per-question time limit in seconds.</summary>
    public int TimeLimit { get; set; } = 20;

    /// <summary>Maximum points awarded for a fully correct, fastest answer.</summary>
    public int Points { get; set; } = 1000;

    [MaxLength(2048)]
    public string? ImageUrl { get; set; }

    // Navigation
    public ICollection<AnswerOption> AnswerOptions { get; set; } = new List<AnswerOption>();
    public ICollection<GameAnswer> GameAnswers { get; set; } = new List<GameAnswer>();
}
