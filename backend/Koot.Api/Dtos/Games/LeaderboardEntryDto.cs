namespace Koot.Api.Dtos.Games;

public class LeaderboardEntryDto
{
    public int Rank { get; set; }
    public int ParticipantId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public int AvatarId { get; set; }
    public int TotalScore { get; set; }
}
