using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Koot.Api.Controllers;
using Koot.Api.Data;
using Koot.Api.Dtos.Auth;
using Koot.Api.Models;
using Koot.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace KootTests;

public class PasswordManagementTests
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
            .Returns(("test.jwt.token", DateTime.UtcNow.AddDays(7)));
        return mock;
    }

    private static IConfiguration MakeConfig()
        => new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Jwt:RefreshTokenExpiryDays"] = "30" }).Build();

    private sealed class CapturingEmailService : IEmailService
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = new();

        public Task SendAsync(string to, string subject, string body)
        {
            Sent.Add((to, subject, body));
            return Task.CompletedTask;
        }
    }

    private static AuthController MakeController(
        AppDbContext db,
        IJwtService jwt,
        IEmailService email,
        int? authenticatedUserId = null)
    {
        var ctrl = new AuthController(
            db, jwt, email, NullLogger<AuthController>.Instance, MakeConfig());

        if (authenticatedUserId is int uid)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim("userId", uid.ToString()),
                new Claim(ClaimTypes.NameIdentifier, uid.ToString()),
            }, authenticationType: "Test");
            ctrl.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity),
                },
            };
        }

        return ctrl;
    }

    private static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static async Task<User> SeedUser(AppDbContext db, string password = "password123")
    {
        var user = new User
        {
            Username = "alice",
            Email = "alice@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task SeedRefreshToken(AppDbContext db, int userId)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = HashToken(raw),
            ExpiresAt = DateTime.UtcNow.AddDays(30),
        });
        await db.SaveChangesAsync();
    }

    // ─── change-password ──────────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_RejectsWrongCurrentPassword()
    {
        await using var db = MakeDb("change_password_wrong_current");
        var user = await SeedUser(db);
        var ctrl = MakeController(db, MakeJwt().Object, new CapturingEmailService(), user.Id);

        var result = await ctrl.ChangePassword(new ChangePasswordRequest
        {
            CurrentPassword = "wrong_password",
            NewPassword = "new_password_123",
        });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value.Should().BeEquivalentTo(new { error = "Current password is incorrect." });

        // Password unchanged
        var fresh = await db.Users.FindAsync(user.Id);
        BCrypt.Net.BCrypt.Verify("password123", fresh!.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task ChangePassword_Succeeds_AndRevokesRefreshTokens()
    {
        await using var db = MakeDb("change_password_success");
        var user = await SeedUser(db);
        await SeedRefreshToken(db, user.Id);
        await SeedRefreshToken(db, user.Id);
        var ctrl = MakeController(db, MakeJwt().Object, new CapturingEmailService(), user.Id);

        var result = await ctrl.ChangePassword(new ChangePasswordRequest
        {
            CurrentPassword = "password123",
            NewPassword = "new_password_456",
        });

        result.Should().BeOfType<NoContentResult>();

        var fresh = await db.Users.FindAsync(user.Id);
        BCrypt.Net.BCrypt.Verify("new_password_456", fresh!.PasswordHash).Should().BeTrue();

        var tokens = await db.RefreshTokens.Where(t => t.UserId == user.Id).ToListAsync();
        tokens.Should().HaveCount(2);
        tokens.Should().OnlyContain(t => t.RevokedAt != null);
    }

    // ─── forgot-password ──────────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_ReturnsOk_WhenEmailUnknown()
    {
        await using var db = MakeDb("forgot_password_unknown_email");
        var email = new CapturingEmailService();
        var ctrl = MakeController(db, MakeJwt().Object, email);

        var result = await ctrl.ForgotPassword(new ForgotPasswordRequest
        {
            Email = "nobody@example.com",
        });

        result.Should().BeOfType<OkResult>();
        email.Sent.Should().BeEmpty();
        (await db.PasswordResetTokens.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ForgotPassword_ReturnsOk_WhenEmailKnown_AndSendsEmail()
    {
        await using var db = MakeDb("forgot_password_known_email");
        var user = await SeedUser(db);
        var email = new CapturingEmailService();
        var ctrl = MakeController(db, MakeJwt().Object, email);

        var result = await ctrl.ForgotPassword(new ForgotPasswordRequest
        {
            Email = "ALICE@EXAMPLE.COM",
        });

        result.Should().BeOfType<OkResult>();
        email.Sent.Should().ContainSingle();
        email.Sent[0].To.Should().Be("alice@example.com");

        var stored = await db.PasswordResetTokens.FirstAsync(t => t.UserId == user.Id);
        stored.UsedAt.Should().BeNull();
        stored.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        // The stored hash should NOT be the raw token (defence in depth).
        stored.TokenHash.Should().NotContain(" ");
    }

    // ─── reset-password ───────────────────────────────────────────────────────

    /// <summary>
    /// Mimics the forgot-password flow by issuing a real token via the controller,
    /// then capturing the raw value out of the email body. Keeps the tests
    /// honest about the actual hashing scheme.
    /// </summary>
    private static async Task<string> IssueResetTokenAsync(
        AuthController ctrl, CapturingEmailService email, string emailAddress)
    {
        var before = email.Sent.Count;
        var result = await ctrl.ForgotPassword(new ForgotPasswordRequest { Email = emailAddress });
        result.Should().BeOfType<OkResult>();
        email.Sent.Count.Should().Be(before + 1);

        // Body format from the controller is "...token: <RAW>"; pull the last word.
        var body = email.Sent[^1].Body;
        var idx = body.LastIndexOf(' ');
        idx.Should().BeGreaterThan(-1);
        return body[(idx + 1)..];
    }

    [Fact]
    public async Task ResetPassword_HappyPath_WorksOnce()
    {
        await using var db = MakeDb("reset_password_happy");
        var user = await SeedUser(db);
        await SeedRefreshToken(db, user.Id);
        var email = new CapturingEmailService();
        var ctrl = MakeController(db, MakeJwt().Object, email);

        var rawToken = await IssueResetTokenAsync(ctrl, email, user.Email);

        var firstReset = await ctrl.ResetPassword(new ResetPasswordRequest
        {
            Token = rawToken,
            NewPassword = "brand_new_pw",
        });
        firstReset.Should().BeOfType<NoContentResult>();

        // Password updated
        var fresh = await db.Users.FindAsync(user.Id);
        BCrypt.Net.BCrypt.Verify("brand_new_pw", fresh!.PasswordHash).Should().BeTrue();

        // Refresh tokens revoked
        var refresh = await db.RefreshTokens.Where(t => t.UserId == user.Id).ToListAsync();
        refresh.Should().OnlyContain(t => t.RevokedAt != null);

        // Token marked as used
        var prt = await db.PasswordResetTokens.FirstAsync(t => t.UserId == user.Id);
        prt.UsedAt.Should().NotBeNull();

        // Second use of the same token must be rejected
        var secondReset = await ctrl.ResetPassword(new ResetPasswordRequest
        {
            Token = rawToken,
            NewPassword = "another_new_pw",
        });
        var bad = secondReset.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value.Should().BeEquivalentTo(new { error = "Invalid or expired reset token." });
    }

    [Fact]
    public async Task ResetPassword_RejectsExpiredToken()
    {
        await using var db = MakeDb("reset_password_expired");
        var user = await SeedUser(db);

        // Manually seed an already-expired token using the same hash scheme
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = HashToken(raw),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
        });
        await db.SaveChangesAsync();

        var ctrl = MakeController(db, MakeJwt().Object, new CapturingEmailService());

        var result = await ctrl.ResetPassword(new ResetPasswordRequest
        {
            Token = raw,
            NewPassword = "whatever_123",
        });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value.Should().BeEquivalentTo(new { error = "Invalid or expired reset token." });

        // Password unchanged
        var fresh = await db.Users.FindAsync(user.Id);
        BCrypt.Net.BCrypt.Verify("password123", fresh!.PasswordHash).Should().BeTrue();
    }
}
