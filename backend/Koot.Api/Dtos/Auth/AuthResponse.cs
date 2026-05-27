namespace Koot.Api.Dtos.Auth;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Raw refresh token. Stored by the client in localStorage (consistent with the
    /// existing JWT approach). Only the SHA-256 hash is persisted server-side.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
}
