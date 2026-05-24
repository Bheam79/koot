using System.ComponentModel.DataAnnotations;
using Koot.Api.Models;

namespace Koot.Api.Dtos.Quizzes;

public class CreateQuestionRequest
{
    public QuestionType Type { get; set; } = QuestionType.MultipleChoice;

    [Required, MaxLength(1000)]
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>Per-question time limit in seconds (5-120).</summary>
    [Range(5, 120)]
    public int TimeLimit { get; set; } = 20;

    /// <summary>Maximum points (100-2000).</summary>
    [Range(100, 2000)]
    public int Points { get; set; } = 1000;

    [MaxLength(2048)]
    public string? ImageUrl { get; set; }

    public int? OrderIndex { get; set; }

    /// <summary>
    /// Answer options.
    /// - MultipleChoice: 2-4 options, at least one IsCorrect.
    /// - TrueFalse: ignored; True/False options are auto-created.
    /// - TypeAnswer: a single option whose Text is the correct answer.
    /// - Poll: 2-4 options, IsCorrect is ignored.
    /// </summary>
    public List<CreateAnswerOptionRequest> AnswerOptions { get; set; } = new();
}
