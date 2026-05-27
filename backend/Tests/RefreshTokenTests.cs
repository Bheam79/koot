using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Koot.Api.Controllers;
using Koot.Api.Data;
using Koot.Api.Dtos.Auth;
using Koot.Api.Models;
using Koot.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace KootTests;

public class RefreshTokenTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static AppDbContext MakeDb(string name)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(opts);
    }

    private static Mock<IJwtService> MakeJwt()
    {
        var mock = new Mock<IJwtService>();
        mock.Setup(j => j.IssueToken(It.IsAny<User>()))
            .Returns(("new.jwt.token", DateTime.UtcNow.AddDays(7)));
        return mock;
    }

    private static IConfiguration MakeConfig(int refreshExpiryDays = 30)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:RefreshTokenExpiryDays"] = refreshExpiryDays.ToString(),
        };
        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    private sealed class NullEmailService : IEmailService
    {
        public Task SendAsync(string to, string subject, string body) => Task.CompletedTask;
    }

    private static AuthController MakeController(AppDbContext db, IJwtService jwt, int refreshExpiryDays = 30)
        => new(db, jwt, new NullEmailService(), NullLogger<AuthController>.Instance, MakeConfig(refreshExpiryDays));

    /// <summary>Mimics the hash logic in AuthController so tests can pre-seed tokens.</summary>
    private static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>Seeds a user and a RefreshToken record; returns the user and raw token string.</summary>
    private static async Task<(User user, string rawToken)> SeedUserWithRefreshToken(
        AppDbContext db,
        DateTime? expiresAt = null,
        DateTime? revokedAt = null)
    {
        var user = new User
        {
            Username = "alice",
            Email = "alice@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var record = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(rawToken),
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(30),
            RevokedAt = revokedAt,
        };
        db.RefreshTokens.Add(record);
        await db.SaveChangesAsync();

        return (user, rawToken);
    }

    // ─── Refresh: valid token rotates correctly ───────────────────────────────

    [Fact]
    public async Task Refresh_ValidToken_ReturnsNewJwtAndRefreshToken()
    {
        await using var db = MakeDb("refresh_valid_returns_new_pair");
        var (_, rawToken) = await SeedUserWithRefreshToken(db);

        var ctrl = MakeController(db, MakeJwt().Object);
        var result = await ctrl.Refresh(new RefreshTokenRequest { RefreshToken = rawToken });

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<AuthResponse>().Subject;
        response.Token.Should().Be("new.jwt.token");
        response.RefreshToken.Should().NotBeNullOrWhiteSpace();
        response.RefreshToken.Should().NotBe(rawToken);
    }

    [Fact]
    public async Task Refresh_ValidToken_OldTokenIsRevoked()
    {
        await using var db = MakeDb("refresh_valid_old_revoked");
        var (_, rawToken) = await SeedUserWithRefreshToken(db);
        var oldHash = HashToken(rawToken);

        var ctrl = MakeController(db, MakeJwt().Object);
        await ctrl.Refresh(new RefreshTokenRequest { RefreshToken = rawToken });

        var oldRecord = await db.RefreshTokens.FirstAsync(t => t.TokenHash == oldHash);
        oldRecord.RevokedAt.Should().NotBeNull();
        oldRecord.ReplacedByTokenId.Should().NotBeNull();
    }

    [Fact]
    public async Task Refresh_ValidToken_OldTokenLinkedToNewToken()
    {
        await using var db = MakeDb("refresh_valid_linked_to_new");
        var (_, rawToken) = await SeedUserWithRefreshToken(db);
        var oldHash = HashToken(rawToken);

        var ctrl = MakeController(db, MakeJwt().Object);
        var result = await ctrl.Refresh(new RefreshTokenRequest { RefreshToken = rawToken });

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<AuthResponse>().Subject;

        var newHash = HashToken(response.RefreshToken);
        var newRecord = await db.RefreshTokens.FirstAsync(t => t.TokenHash == newHash);
        var oldRecord = await db.RefreshTokens.FirstAsync(t => t.TokenHash == oldHash);

        oldRecord.ReplacedByTokenId.Should().Be(newRecord.Id);
    }

    [Fact]
    public async Task Refresh_ValidToken_NewTokenIsNotRevoked()
    {
        await using var db = MakeDb("refresh_valid_new_not_revoked");
        var (_, rawToken) = await SeedUserWithRefreshToken(db);

        var ctrl = MakeController(db, MakeJwt().Object);
        var result = await ctrl.Refresh(new RefreshTokenRequest { RefreshToken = rawToken });

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<AuthResponse>().Subject;
        var newHash = HashToken(response.RefreshToken);

        var newRecord = await db.RefreshTokens.FirstAsync(t => t.TokenHash == newHash);
        newRecord.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task Refresh_ValidToken_OldTokenNowRejected()
    {
        await using var db = MakeDb("refresh_valid_old_rejected_on_reuse");
        var (_, rawToken) = await SeedUserWithRefreshToken(db);

        var ctrl = MakeController(db, MakeJwt().Object);

        // First rotation succeeds
        var first = await ctrl.Refresh(new RefreshTokenRequest { RefreshToken = rawToken });
        first.Result.Should().BeOfType<OkObjectResult>();

        // Reusing the now-revoked token must fail
        var second = await ctrl.Refresh(new RefreshTokenRequest { RefreshToken = rawToken });
        second.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ─── Refresh: revoked token returns 401 ──────────────────────────────────

    [Fact]
    public async Task Refresh_RevokedToken_Returns401()
    {
        await using var db = MakeDb("refresh_revoked_401");
        var (_, rawToken) = await SeedUserWithRefreshToken(db, revokedAt: DateTime.UtcNow.AddHours(-1));

        var ctrl = MakeController(db, MakeJwt().Object);
        var result = await ctrl.Refresh(new RefreshTokenRequest { RefreshToken = rawToken });

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ─── Refresh: expired token returns 401 ──────────────────────────────────

    [Fact]
    public async Task Refresh_ExpiredToken_Returns401()
    {
        await using var db = MakeDb("refresh_expired_401");
        var (_, rawToken) = await SeedUserWithRefreshToken(db, expiresAt: DateTime.UtcNow.AddSeconds(-1));

        var ctrl = MakeController(db, MakeJwt().Object);
        var result = await ctrl.Refresh(new RefreshTokenRequest { RefreshToken = rawToken });

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ─── Refresh: unknown token returns 401 ──────────────────────────────────

    [Fact]
    public async Task Refresh_UnknownToken_Returns401()
    {
        await using var db = MakeDb("refresh_unknown_401");
        // No tokens in DB
        var user = new User
        {
            Username = "alice",
            Email = "alice@example.com",
            PasswordHash = "irrelevant",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var ctrl = MakeController(db, MakeJwt().Object);
        var result = await ctrl.Refresh(new RefreshTokenRequest { RefreshToken = "not-a-real-token" });

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }
}
