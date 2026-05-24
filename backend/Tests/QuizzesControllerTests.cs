using System.Security.Claims;
using FluentAssertions;
using Koot.Api.Controllers;
using Koot.Api.Data;
using Koot.Api.Dtos.Quizzes;
using Koot.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace KootTests;

public class QuizzesControllerTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static AppDbContext MakeDb(string name)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(opts);
    }

    private static QuizzesController MakeController(AppDbContext db, int userId)
    {
        var ctrl = new QuizzesController(db, NullLogger<QuizzesController>.Instance);

        var claims = new[]
        {
            new Claim("userId", userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        };
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
            },
        };

        return ctrl;
    }

    private static async Task<AppDbContext> SeedAsync(string name)
    {
        var db = MakeDb(name);

        db.Users.Add(new User { Id = 1, Username = "alice", Email = "alice@example.com", PasswordHash = "h" });
        db.Users.Add(new User { Id = 2, Username = "bob", Email = "bob@example.com", PasswordHash = "h" });

        db.Quizzes.Add(new Quiz
        {
            Id = 1,
            UserId = 1,
            Title = "Alice Quiz",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        db.Quizzes.Add(new Quiz
        {
            Id = 2,
            UserId = 2,
            Title = "Bob Quiz",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
        return db;
    }

    // ─── List ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsOnlyCurrentUserQuizzes()
    {
        await using var db = await SeedAsync("quiz_list");
        var ctrl = MakeController(db, userId: 1);

        var result = await ctrl.List();

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var quizzes = ok.Value.Should().BeAssignableTo<IEnumerable<QuizSummaryDto>>().Subject;
        quizzes.Should().HaveCount(1).And.Contain(q => q.Title == "Alice Quiz");
    }

    // ─── Get ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_ReturnsQuiz_WhenOwned()
    {
        await using var db = await SeedAsync("quiz_get_ok");
        var ctrl = MakeController(db, userId: 1);

        var result = await ctrl.Get(1);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<QuizDetailDto>().Which.Title.Should().Be("Alice Quiz");
    }

    [Fact]
    public async Task Get_ReturnsNotFound_ForMissingQuiz()
    {
        await using var db = await SeedAsync("quiz_get_notfound");
        var ctrl = MakeController(db, userId: 1);

        var result = await ctrl.Get(999);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Get_ReturnsForbid_WhenOwnedByOtherUser()
    {
        await using var db = await SeedAsync("quiz_get_forbid");
        var ctrl = MakeController(db, userId: 1); // Alice tries to get Bob's quiz

        var result = await ctrl.Get(2);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    // ─── Create ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_AddsQuiz_AndReturnsCreatedAt()
    {
        await using var db = await SeedAsync("quiz_create");
        var ctrl = MakeController(db, userId: 1);

        var result = await ctrl.Create(new CreateQuizRequest
        {
            Title = "My new quiz",
            Description = "Test desc",
        });

        result.Result.Should().BeOfType<CreatedAtActionResult>();
        db.Quizzes.Should().Contain(q => q.Title == "My new quiz" && q.UserId == 1);
    }

    [Fact]
    public async Task Create_TrimsTitle()
    {
        await using var db = await SeedAsync("quiz_create_trim");
        var ctrl = MakeController(db, userId: 1);

        await ctrl.Create(new CreateQuizRequest { Title = "  Trimmed Title  " });

        db.Quizzes.Should().Contain(q => q.Title == "Trimmed Title");
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ChangesTitle_WhenOwned()
    {
        await using var db = await SeedAsync("quiz_update_ok");
        var ctrl = MakeController(db, userId: 1);

        var result = await ctrl.Update(1, new UpdateQuizRequest { Title = "Updated Title" });

        result.Result.Should().BeOfType<OkObjectResult>();
        var quiz = await db.Quizzes.FindAsync(1);
        quiz!.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task Update_ReturnsForbid_WhenOwnedByOtherUser()
    {
        await using var db = await SeedAsync("quiz_update_forbid");
        var ctrl = MakeController(db, userId: 1);

        var result = await ctrl.Update(2, new UpdateQuizRequest { Title = "Hack" });

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Update_ReturnsNotFound_ForMissingQuiz()
    {
        await using var db = await SeedAsync("quiz_update_notfound");
        var ctrl = MakeController(db, userId: 1);

        var result = await ctrl.Update(999, new UpdateQuizRequest { Title = "X" });

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    // ─── Delete ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_RemovesQuiz_WhenOwned()
    {
        await using var db = await SeedAsync("quiz_delete_ok");
        var ctrl = MakeController(db, userId: 1);

        var result = await ctrl.Delete(1);

        result.Should().BeOfType<NoContentResult>();
        db.Quizzes.Should().NotContain(q => q.Id == 1);
    }

    [Fact]
    public async Task Delete_ReturnsForbid_WhenOwnedByOtherUser()
    {
        await using var db = await SeedAsync("quiz_delete_forbid");
        var ctrl = MakeController(db, userId: 1);

        var result = await ctrl.Delete(2);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_ForMissingQuiz()
    {
        await using var db = await SeedAsync("quiz_delete_notfound");
        var ctrl = MakeController(db, userId: 1);

        var result = await ctrl.Delete(999);

        result.Should().BeOfType<NotFoundResult>();
    }
}
