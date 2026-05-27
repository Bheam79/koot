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
    private readonly ILogger<AuthController> _logger;
    private readonly int _refreshTokenExpiryDays;

    public AuthController(
        AppDbContext db,
        IJwtService jwt,
        ILogger<AuthController> logger,
        IConfiguration configuration)
    {
        _db = db;
        _jwt = jwt;
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
}
