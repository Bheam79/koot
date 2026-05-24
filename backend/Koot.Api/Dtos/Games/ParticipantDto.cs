namespace Koot.Api.Dtos.Games;

public class ParticipantDto
{
    public int Id { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public int AvatarId { get; set; }
    public int TotalScore { get; set; }
    public bool IsDisconnected { get; set; }
    public DateTime JoinedAt { get; set; }
}
