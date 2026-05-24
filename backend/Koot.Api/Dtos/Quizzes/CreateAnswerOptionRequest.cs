using System.ComponentModel.DataAnnotations;

namespace Koot.Api.Dtos.Quizzes;

public class CreateAnswerOptionRequest
{
    [Required, MaxLength(500)]
    public string Text { get; set; } = string.Empty;

    public bool IsCorrect { get; set; }

    public int OrderIndex { get; set; }
}
