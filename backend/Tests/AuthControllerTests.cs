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

public class AuthControllerTests
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

    private static AuthController MakeController(AppDbContext db, IJwtService jwt)
        => new(db, jwt, NullLogger<AuthController>.Instance, MakeConfig());

    // ─── Register ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ReturnsOk_WithToken_OnSuccess()
    {
        await using var db = MakeDb("auth_register_ok");
        var ctrl = MakeController(db, MakeJwt().Object);

        var request = new RegisterRequest
        {
            Username = "alice",
            Email = "alice@example.com",
            Password = "password123",
        };

        var result = await ctrl.Register(request);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<AuthResponse>().Subject;
        response.Token.Should().Be("test.jwt.token");
        response.Username.Should().Be("alice");
        response.Email.Should().Be("alice@example.com");
    }

    [Fact]
    public async Task Register_ReturnsOk_WithRefreshToken()
    {
        await using var db = MakeDb("auth_register_refresh_token");
        var ctrl = MakeController(db, MakeJwt().Object);

        var result = await ctrl.Register(new RegisterRequest
        {
            Username = "alice",
            Email = "alice@example.com",
            Password = "password123",
        });

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<AuthResponse>().Subject;
        response.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Register_ReturnsConflict_WhenEmailAlreadyTaken()
    {
        await using var db = MakeDb("auth_register_email_conflict");
        db.Users.Add(new User
        {
            Username = "existing",
            Email = "existing@example.com",
            PasswordHash = "hash",
        });
        await db.SaveChangesAsync();

        var ctrl = MakeController(db, MakeJwt().Object);

        var result = await ctrl.Register(new RegisterRequest
        {
            Username = "new_user",
            Email = "EXISTING@EXAMPLE.COM", // normalised → match
            Password = "password123",
        });

        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Register_ReturnsConflict_WhenUsernameAlreadyTaken()
    {
        await using var db = MakeDb("auth_register_username_conflict");
        db.Users.Add(new User
        {
            Username = "alice",
            Email = "alice@example.com",
            PasswordHash = "hash",
        });
        await db.SaveChangesAsync();

        var ctrl = MakeController(db, MakeJwt().Object);

        var result = await ctrl.Register(new RegisterRequest
        {
            Username = "alice",
            Email = "other@example.com",
            Password = "password123",
        });

        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Register_PersistsUserWithHashedPassword()
    {
        await using var db = MakeDb("auth_register_hash");
        var ctrl = MakeController(db, MakeJwt().Object);

        await ctrl.Register(new RegisterRequest
        {
            Username = "bob",
            Email = "bob@example.com",
            Password = "s3cr3t!",
        });

        var saved = await db.Users.FirstAsync(u => u.Email == "bob@example.com");
        saved.PasswordHash.Should().NotBe("s3cr3t!");
        BCrypt.Net.BCrypt.Verify("s3cr3t!", saved.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task Register_NormalisesEmail_ToLowercase()
    {
        await using var db = MakeDb("auth_register_normalise");
        var ctrl = MakeController(db, MakeJwt().Object);

        await ctrl.Register(new RegisterRequest
        {
            Username = "charlie",
            Email = "  Charlie@Example.COM  ",
            Password = "password123",
        });

        db.Users.Should().ContainSingle(u => u.Email == "charlie@example.com");
    }

    // ─── Login ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ReturnsOk_WithToken_OnValidCredentials()
    {
        await using var db = MakeDb("auth_login_ok");
        db.Users.Add(new User
        {
            Username = "alice",
            Email = "alice@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
        });
        await db.SaveChangesAsync();

        var ctrl = MakeController(db, MakeJwt().Object);

        var result = await ctrl.Login(new LoginRequest
        {
            Email = "alice@example.com",
            Password = "password123",
        });

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<AuthResponse>().Subject;
        response.Token.Should().Be("test.jwt.token");
    }

    [Fact]
    public async Task Login_ReturnsOk_WithRefreshToken()
    {
        await using var db = MakeDb("auth_login_refresh_token");
        db.Users.Add(new User
        {
            Username = "alice",
            Email = "alice@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
        });
        await db.SaveChangesAsync();

        var ctrl = MakeController(db, MakeJwt().Object);

        var result = await ctrl.Login(new LoginRequest
        {
            Email = "alice@example.com",
            Password = "password123",
        });

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<AuthResponse>().Subject;
        response.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenUserNotFound()
    {
        await using var db = MakeDb("auth_login_notfound");
        var ctrl = MakeController(db, MakeJwt().Object);

        var result = await ctrl.Login(new LoginRequest
        {
            Email = "nobody@example.com",
            Password = "password123",
        });

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenPasswordWrong()
    {
        await using var db = MakeDb("auth_login_wrongpassword");
        db.Users.Add(new User
        {
            Username = "alice",
            Email = "alice@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct_password"),
        });
        await db.SaveChangesAsync();

        var ctrl = MakeController(db, MakeJwt().Object);

        var result = await ctrl.Login(new LoginRequest
        {
            Email = "alice@example.com",
            Password = "wrong_password",
        });

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_NormalisesEmail_BeforeLookup()
    {
        await using var db = MakeDb("auth_login_normalise");
        db.Users.Add(new User
        {
            Username = "alice",
            Email = "alice@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
        });
        await db.SaveChangesAsync();

        var ctrl = MakeController(db, MakeJwt().Object);

        var result = await ctrl.Login(new LoginRequest
        {
            Email = "ALICE@EXAMPLE.COM",
            Password = "password123",
        });

        result.Result.Should().BeOfType<OkObjectResult>();
    }
}
