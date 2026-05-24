using System.ComponentModel.DataAnnotations;

namespace Koot.Api.Models;

public class Quiz
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(2048)]
    public string? CoverImageUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsPublic { get; set; }

    // Navigation
    public ICollection<Question> Questions { get; set; } = new List<Question>();
    public ICollection<GameSession> Sessions { get; set; } = new List<GameSession>();
}
