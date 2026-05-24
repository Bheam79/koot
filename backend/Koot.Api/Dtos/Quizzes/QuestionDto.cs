using Koot.Api.Models;

namespace Koot.Api.Dtos.Quizzes;

public class QuestionDto
{
    public int Id { get; set; }
    public QuestionType Type { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public int TimeLimit { get; set; }
    public int Points { get; set; }
    public string? ImageUrl { get; set; }
    public int OrderIndex { get; set; }
    public List<AnswerOptionDto> AnswerOptions { get; set; } = new();
}
