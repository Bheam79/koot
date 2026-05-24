using System.ComponentModel.DataAnnotations;
using Koot.Api.Models;

namespace Koot.Api.Dtos.Quizzes;

public class UpdateQuestionRequest
{
    public QuestionType Type { get; set; } = QuestionType.MultipleChoice;

    [Required, MaxLength(1000)]
    public string QuestionText { get; set; } = string.Empty;

    [Range(5, 120)]
    public int TimeLimit { get; set; } = 20;

    [Range(100, 2000)]
    public int Points { get; set; } = 1000;

    [MaxLength(2048)]
    public string? ImageUrl { get; set; }

    public int? OrderIndex { get; set; }

    /// <summary>
    /// Replacement answer options for the question (full set; existing options are replaced).
    /// Same per-type rules as create.
    /// </summary>
    public List<CreateAnswerOptionRequest> AnswerOptions { get; set; } = new();
}
