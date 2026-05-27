using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Koot.Api.Data;
using Koot.Api.Dtos.Auth;
using Koot.Api.Models;
using Koot.Api.Options;
using Koot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Koot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IJwtService _jwt;
    private readonly IEmailService _email;
    private readonly ILogger<AuthController> _logger;
    private readonly int _refreshTokenExpiryDays;

    public AuthController(
        AppDbContext db,
        IJwtService jwt,
        IEmailService email,
        ILogger<AuthController> logger,
        IConfiguration configuration)
    {
        _db = db;
        _jwt = jwt;
        _email = email;
        _logger = logger;
        _refreshTokenExpiryDays = configuration.GetValue<int?>("Jwt:RefreshTokenExpiryDays") ?? 30;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a cryptographically random token, persists only its SHA-256 hash,
    /// and returns the raw token for transmission to the client.
    /// </summary>
    private async Task<string> IssueRefreshTokenAsync(int userId)
    {
        // 32 random bytes → 44-char base64 raw token sent to client
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var hash = HashToken(raw);

        var record = new RefreshToken
        {
            UserId = userId,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenExpiryDays),
        };

        _db.RefreshTokens.Add(record);
        await _db.SaveChangesAsync();

        return raw;
    }

    /// <summary>Returns the hex-encoded SHA-256 hash of <paramref name="rawToken"/>.</summary>
    private static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // ─── Register ───────────────────────────────────────────────────────────

    /// <summary>
    /// Register a new user. Hashes the password with BCrypt, persists the user,
    /// then returns a JWT and refresh token.
    /// </summary>
    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Register)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedUsername = request.Username.Trim();

        var emailTaken = await _db.Users.AnyAsync(u => u.Email == normalizedEmail);
        if (emailTaken)
        {
            return Conflict(new { error = "A user with that email already exists." });
        }

        var usernameTaken = await _db.Users.AnyAsync(u => u.Username == normalizedUsername);
        if (usernameTaken)
        {
            return Conflict(new { error = "That username is already taken." });
        }

        var user = new User
        {
            Username = normalizedUsername,
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Registered new user {UserId} ({Username})", user.Id, user.Username);

        var (token, expires) = _jwt.IssueToken(user);
        var refreshToken = await IssueRefreshTokenAsync(user.Id);

        return Ok(new AuthResponse
        {
            Token = token,
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            ExpiresAt = expires,
            RefreshToken = refreshToken,
        });
    }

    // ─── Login ──────────────────────────────────────────────────────────────

    /// <summary>Validate credentials and return a JWT and refresh token.</summary>
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            // Same response shape for missing user vs bad password so we don't leak which.
            return Unauthorized(new { error = "Invalid email or password." });
        }

        var (token, expires) = _jwt.IssueToken(user);
        var refreshToken = await IssueRefreshTokenAsync(user.Id);

        return Ok(new AuthResponse
        {
            Token = token,
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            ExpiresAt = expires,
            RefreshToken = refreshToken,
        });
    }

    // ─── Refresh ────────────────────────────────────────────────────────────

    /// <summary>
    /// Exchange a valid refresh token for a new access JWT and a rotated refresh token.
    ///
    /// Token rotation: the old refresh token is revoked and a new one is issued.
    /// The old record's ReplacedByTokenId points to the new record for audit purposes.
    /// Presenting a revoked token always returns 401 — this detects token theft via
    /// replay of an already-rotated token.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshTokenRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var hash = HashToken(request.RefreshToken);
        var now = DateTime.UtcNow;

        var existing = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (existing is null || existing.RevokedAt is not null || existing.ExpiresAt <= now)
        {
            return Unauthorized(new { error = "Invalid or expired refresh token." });
        }

        // Issue new pair
        var (newJwt, expires) = _jwt.IssueToken(existing.User);
        var newRawToken = await IssueRefreshTokenAsync(existing.UserId);

        // Revoke old token and link it to the new one
        var newRecord = await _db.RefreshTokens
            .FirstAsync(t => t.TokenHash == HashToken(newRawToken));

        existing.RevokedAt = now;
        existing.ReplacedByTokenId = newRecord.Id;
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Refresh token rotated for user {UserId} (old={OldId} new={NewId})",
            existing.UserId, existing.Id, newRecord.Id);

        return Ok(new AuthResponse
        {
            Token = newJwt,
            UserId = existing.User.Id,
            Username = existing.User.Username,
            Email = existing.User.Email,
            ExpiresAt = expires,
            RefreshToken = newRawToken,
        });
    }

    // ─── Me ─────────────────────────────────────────────────────────────────

    /// <summary>Return the currently authenticated user's profile.</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me()
    {
        var idClaim = User.FindFirstValue("userId")
                      ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(idClaim, out var userId))
        {
            return Unauthorized();
        }

        var user = await _db.Users.FindAsync(userId);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(new MeResponse
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            CreatedAt = user.CreatedAt,
        });
    }

    // ─── Change password ────────────────────────────────────────────────────

    /// <summary>
    /// Change the authenticated user's password. Verifies the current password,
    /// stores the new BCrypt hash, and revokes all of the user's outstanding
    /// refresh tokens so other sessions are forced to re-login.
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!CurrentUser.TryGetId(User, out var userId))
        {
            return Unauthorized();
        }

        var user = await _db.Users.FindAsync(userId);
        if (user is null)
        {
            return Unauthorized();
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return BadRequest(new { error = "Current password is incorrect." });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        var now = DateTime.UtcNow;
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync();
        foreach (var rt in activeTokens)
        {
            rt.RevokedAt = now;
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "User {UserId} changed password; revoked {Count} active refresh token(s)",
            userId, activeTokens.Count);

        return NoContent();
    }

    // ─── Forgot password ────────────────────────────────────────────────────

    /// <summary>
    /// Initiate a password-reset flow. Always returns 200 — we never reveal
    /// whether an account exists for that email (prevents user enumeration).
    /// If the user exists, a one-hour reset token is generated, its hash
    /// persisted, and the raw token dispatched via <see cref="IEmailService"/>.
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user is not null)
        {
            // 32 random bytes → base64url so the token is safe to drop in a URL
            var raw = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

            var record = new PasswordResetToken
            {
                UserId = user.Id,
                TokenHash = HashToken(raw),
                ExpiresAt = DateTime.UtcNow.AddHours(1),
            };
            _db.PasswordResetTokens.Add(record);
            await _db.SaveChangesAsync();

            await _email.SendAsync(
                user.Email,
                "Reset your Koot password",
                $"Use the following token to reset your password (valid for 1 hour): {raw}");

            _logger.LogInformation(
                "Password reset token issued for user {UserId}", user.Id);
        }
        else
        {
            _logger.LogInformation(
                "Password reset requested for unknown email {Email}", normalizedEmail);
        }

        // Always 200 — don't reveal whether the email exists.
        return Ok();
    }

    // ─── Reset password ─────────────────────────────────────────────────────

    /// <summary>
    /// Consume a password-reset token and set a new password. The token is
    /// looked up by hash; on success the user's password is updated, the
    /// token is marked used, and all active refresh tokens for the user are
    /// revoked.
    /// </summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var hash = HashToken(request.Token);
        var now = DateTime.UtcNow;

        var token = await _db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash
                                       && t.UsedAt == null
                                       && t.ExpiresAt > now);

        if (token is null)
        {
            return BadRequest(new { error = "Invalid or expired reset token." });
        }

        token.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        token.UsedAt = now;

        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == token.UserId && t.RevokedAt == null)
            .ToListAsync();
        foreach (var rt in activeTokens)
        {
            rt.RevokedAt = now;
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Password reset completed for user {UserId}; revoked {Count} active refresh token(s)",
            token.UserId, activeTokens.Count);

        return NoContent();
    }

    /// <summary>
    /// Encode <paramref name="bytes"/> as URL-safe base64 (RFC 4648 §5) — i.e. no
    /// padding, '-' instead of '+', '_' instead of '/'.
    /// </summary>
    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
