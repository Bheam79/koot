using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Koot.Api.Models;
using Koot.Api.Services;
using Microsoft.Extensions.Configuration;

namespace KootTests;

public class JwtServiceTests
{
    private static JwtService CreateService(
        string? expiryDays = "7",
        string? expiryMinutes = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience",
            ["Jwt:Key"] = "super-secret-key-that-is-long-enough-for-hmac256",
        };
        if (expiryDays is not null)
            settings["Jwt:ExpiryDays"] = expiryDays;
        if (expiryMinutes is not null)
            settings["Jwt:ExpiryMinutes"] = expiryMinutes;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return new JwtService(config);
    }

    private static User MakeUser() => new()
    {
        Id = 42,
        Username = "alice",
        Email = "alice@example.com",
        PasswordHash = "irrelevant",
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public void IssueToken_ReturnsNonEmptyToken()
    {
        var svc = CreateService();
        var (token, _) = svc.IssueToken(MakeUser());
        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void IssueToken_ExpiresAt_IsInFuture()
    {
        var before = DateTime.UtcNow;
        var svc = CreateService(expiryDays: "7");
        var (_, expiresAt) = svc.IssueToken(MakeUser());
        expiresAt.Should().BeAfter(before.AddDays(6));
        expiresAt.Should().BeBefore(before.AddDays(8));
    }

    [Fact]
    public void IssueToken_FallsBackToExpiryMinutes_WhenNoDays()
    {
        var before = DateTime.UtcNow;
        var svc = CreateService(expiryDays: null, expiryMinutes: "30");
        var (_, expiresAt) = svc.IssueToken(MakeUser());
        expiresAt.Should().BeAfter(before.AddMinutes(29));
        expiresAt.Should().BeBefore(before.AddMinutes(31));
    }

    [Fact]
    public void IssueToken_TokenContains_UserClaims()
    {
        var svc = CreateService();
        var user = MakeUser();
        var (tokenString, _) = svc.IssueToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenString);

        jwt.Claims.Should().Contain(c => c.Type == "userId" && c.Value == "42");
        jwt.Claims.Should().Contain(c => c.Type == "username" && c.Value == "alice");
        jwt.Claims.Should().Contain(c =>
            (c.Type == JwtRegisteredClaimNames.Email ||
             c.Type == ClaimTypes.Email) && c.Value == "alice@example.com");
    }

    [Fact]
    public void IssueToken_TokenHas_CorrectIssuerAndAudience()
    {
        var svc = CreateService();
        var (tokenString, _) = svc.IssueToken(MakeUser());

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenString);

        jwt.Issuer.Should().Be("test-issuer");
        jwt.Audiences.Should().Contain("test-audience");
    }

    [Fact]
    public void Constructor_Throws_WhenJwtKeyMissing()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "i",
                ["Jwt:Audience"] = "a",
                // No Key
            })
            .Build();

        var act = () => new JwtService(config);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt:Key*");
    }

    [Fact]
    public void IssueToken_DefaultsToSixtyMinutes_WhenNeitherDaysNorMinutes()
    {
        var before = DateTime.UtcNow;
        // Provide neither ExpiryDays nor ExpiryMinutes
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "i",
                ["Jwt:Audience"] = "a",
                ["Jwt:Key"] = "super-secret-key-that-is-long-enough-for-hmac256",
            })
            .Build();
        var svc = new JwtService(config);
        var (_, expiresAt) = svc.IssueToken(MakeUser());
        expiresAt.Should().BeAfter(before.AddMinutes(59));
        expiresAt.Should().BeBefore(before.AddMinutes(61));
    }
}
