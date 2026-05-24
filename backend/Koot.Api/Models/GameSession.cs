using System.ComponentModel.DataAnnotations;

namespace Koot.Api.Models;

public enum GameStatus
{
    Lobby = 0,
    InProgress = 1,
    Finished = 2,
}

public class GameSession
{
    public int Id { get; set; }

    public int QuizId { get; set; }
    public Quiz? Quiz { get; set; }

    public int HostUserId { get; set; }
    public User? HostUser { get; set; }

    /// <summary>6-character join code (unique while session is active).</summary>
    [Required, MaxLength(6), MinLength(6)]
    public string Code { get; set; } = string.Empty;

    public GameStatus Status { get; set; } = GameStatus.Lobby;

    public int CurrentQuestionIndex { get; set; } = -1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    // Navigation
    public ICollection<GameParticipant> Participants { get; set; } = new List<GameParticipant>();
    public ICollection<GameAnswer> Answers { get; set; } = new List<GameAnswer>();
}
