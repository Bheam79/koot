using System.ComponentModel.DataAnnotations;

namespace Koot.Api.Models;

/// <summary>
/// Persisted refresh token record.
///
/// Storage decision: The raw random token is returned to the client and stored in
/// localStorage (consistent with the existing JWT approach). Only the SHA-256 hash
/// is persisted here so a DB compromise does not expose usable tokens.
/// If the app later needs XSS hardening, migrate to httpOnly cookies + CSRF
/// double-submit pattern.
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }

    public int UserId { get; set; }

    /// <summary>Hex-encoded SHA-256 hash of the raw token returned to the client.</summary>
    [Required, MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    /// <summary>Set when this token is rotated; points to the replacement record.</summary>
    public int? ReplacedByTokenId { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public RefreshToken? ReplacedByToken { get; set; }
}
