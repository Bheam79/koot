namespace Koot.Api.Dtos.Quizzes;

public class QuizSummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public int QuestionCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
