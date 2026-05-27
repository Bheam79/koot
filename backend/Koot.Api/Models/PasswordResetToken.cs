using System.ComponentModel.DataAnnotations;

namespace Koot.Api.Models;

/// <summary>
/// Persisted password-reset token. Same storage model as <see cref="RefreshToken"/>:
/// the raw token is emailed to the user, and only its SHA-256 hash is persisted here.
/// One-shot — once UsedAt is non-null the token is consumed.
/// </summary>
public class PasswordResetToken
{
    public int Id { get; set; }

    public int UserId { get; set; }

    /// <summary>Hex-encoded SHA-256 hash of the raw token delivered to the user.</summary>
    [Required, MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
