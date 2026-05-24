using System.ComponentModel.DataAnnotations;

namespace Koot.Api.Models;

public class GameParticipant
{
    public int Id { get; set; }

    public int SessionId { get; set; }
    public GameSession? Session { get; set; }

    [Required, MaxLength(40)]
    public string Nickname { get; set; } = string.Empty;

    /// <summary>Reference into <see cref="Avatars.All"/>, range 1..12.</summary>
    public int AvatarId { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public int TotalScore { get; set; }

    // Navigation
    public ICollection<GameAnswer> Answers { get; set; } = new List<GameAnswer>();
}
