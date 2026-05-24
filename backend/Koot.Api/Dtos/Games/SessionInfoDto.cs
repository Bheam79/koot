namespace Koot.Api.Dtos.Games;

public class SessionInfoDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string QuizTitle { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ParticipantCount { get; set; }
}
