namespace Koot.Api.Dtos.Games;

/// <summary>Answer option sent to players — IsCorrect is intentionally omitted.</summary>
public class AnswerOptionBroadcastDto
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
}
