namespace Koot.Api.Dtos.Games;

public class StartGameResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string JoinUrl { get; set; } = string.Empty;
}
