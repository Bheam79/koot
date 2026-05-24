namespace Koot.Api.Dtos.Games;

public class AnswerResultDto
{
    public int ParticipantId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public int PointsEarned { get; set; }
    public int TotalScore { get; set; }
    public bool IsCorrect { get; set; }
}
