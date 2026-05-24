namespace Koot.Api.Dtos.Games;

/// <summary>Question data sent to players — correct answer flags are stripped.</summary>
public class QuestionBroadcastDto
{
    public int Id { get; set; }
    public int Type { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public int TimeLimit { get; set; }
    public int Points { get; set; }
    public string? ImageUrl { get; set; }
    public int OrderIndex { get; set; }
    public List<AnswerOptionBroadcastDto> AnswerOptions { get; set; } = new();
}
