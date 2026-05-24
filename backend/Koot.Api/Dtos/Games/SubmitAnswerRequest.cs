namespace Koot.Api.Dtos.Games;

public class SubmitAnswerRequest
{
    public string Code { get; set; } = string.Empty;
    public int QuestionId { get; set; }
    public int? AnswerOptionId { get; set; }
    public string? AnswerText { get; set; }
    public int TimeTakenMs { get; set; }
}
