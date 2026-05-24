using System.ComponentModel.DataAnnotations;

namespace Koot.Api.Dtos.Quizzes;

public class CreateQuizRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(2048)]
    public string? CoverImageUrl { get; set; }
}
