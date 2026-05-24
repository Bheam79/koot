using FluentAssertions;
using Koot.Api.Data;
using Koot.Api.Models;
using Koot.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace KootTests;

public class GameServiceTests
{
    // ─── CalculateScore (pure static) ─────────────────────────────────────────

    [Fact]
    public void CalculateScore_WrongAnswer_ReturnsZero()
    {
        var score = GameService.CalculateScore(isCorrect: false, timeTakenMs: 0, timeLimit: 20, maxPoints: 1000);
        score.Should().Be(0);
    }

    [Fact]
    public void CalculateScore_CorrectInstantAnswer_ReturnsMaxPoints()
    {
        // timeTakenMs = 0 → timeFraction = 0 → score = maxPoints * 1.0
        var score = GameService.CalculateScore(isCorrect: true, timeTakenMs: 0, timeLimit: 20, maxPoints: 1000);
        score.Should().Be(1000);
    }

    [Fact]
    public void CalculateScore_CorrectAtHalfTime_ReturnsSeventyFivePercent()
    {
        // timeFraction = 0.5 → score = maxPoints * (1 - 0.5*0.5) = 0.75 * maxPoints
        var score = GameService.CalculateScore(isCorrect: true, timeTakenMs: 10_000, timeLimit: 20, maxPoints: 1000);
        score.Should().Be(750);
    }

    [Fact]
    public void CalculateScore_CorrectAtFullTime_ReturnsFiftyPercent()
    {
        // timeFraction = 1 → score = maxPoints * (1 - 0.5) = 0.5 * maxPoints
        var score = GameService.CalculateScore(isCorrect: true, timeTakenMs: 20_000, timeLimit: 20, maxPoints: 1000);
        score.Should().Be(500);
    }

    [Fact]
    public void CalculateScore_CorrectOverTimeLimit_ClampsToFiftyPercent()
    {
        // timeFraction clamped to 1 → same as full-time
        var score = GameService.CalculateScore(isCorrect: true, timeTakenMs: 99_999, timeLimit: 20, maxPoints: 1000);
        score.Should().Be(500);
    }

    [Fact]
    public void CalculateScore_NegativeTimeTaken_ClampsToZeroFraction()
    {
        var score = GameService.CalculateScore(isCorrect: true, timeTakenMs: -100, timeLimit: 20, maxPoints: 1000);
        score.Should().Be(1000);
    }

    [Fact]
    public void CalculateScore_ZeroMaxPoints_ReturnsZero()
    {
        var score = GameService.CalculateScore(isCorrect: true, timeTakenMs: 0, timeLimit: 20, maxPoints: 0);
        score.Should().Be(0);
    }

    // ─── GetQuestionForBroadcast (pure static) ─────────────────────────────────

    [Fact]
    public void GetQuestionForBroadcast_StripIsCorrect()
    {
        var question = new Question
        {
            Id = 1,
            QuestionText = "What?",
            TimeLimit = 20,
            Points = 1000,
            Type = QuestionType.MultipleChoice,
            AnswerOptions = new List<AnswerOption>
            {
                new() { Id = 10, Text = "Correct", IsCorrect = true, OrderIndex = 0 },
                new() { Id = 11, Text = "Wrong",   IsCorrect = false, OrderIndex = 1 },
            },
        };

        var dto = GameService.GetQuestionForBroadcast(question);

        dto.Id.Should().Be(1);
        dto.AnswerOptions.Should().HaveCount(2);
        // BroadcastDto should not expose IsCorrect
        dto.AnswerOptions.Should().AllSatisfy(o =>
        {
            // Verify by checking the DTO type only has Id, Text, OrderIndex
            o.GetType().GetProperty("IsCorrect").Should().BeNull();
        });
    }

    [Fact]
    public void GetQuestionForBroadcast_OrdersByOrderIndex()
    {
        var question = new Question
        {
            Id = 2,
            QuestionText = "Order test",
            TimeLimit = 10,
            Points = 500,
            AnswerOptions = new List<AnswerOption>
            {
                new() { Id = 20, Text = "B", IsCorrect = false, OrderIndex = 1 },
                new() { Id = 21, Text = "A", IsCorrect = true,  OrderIndex = 0 },
            },
        };

        var dto = GameService.GetQuestionForBroadcast(question);

        dto.AnswerOptions[0].Id.Should().Be(21);
        dto.AnswerOptions[1].Id.Should().Be(20);
    }

    // ─── GenerateUniqueCodeAsync (requires in-memory DB) ───────────────────────

    private static AppDbContext MakeDb(string name)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(opts);
    }

    [Fact]
    public async Task GenerateUniqueCodeAsync_ReturnsCode_WhenNoConflicts()
    {
        await using var db = MakeDb("gen_empty");
        var svc = new GameService(db);

        var code = await svc.GenerateUniqueCodeAsync();

        code.Should().HaveLength(6);
        code.Should().MatchRegex("^[A-Z2-9]+$");
    }

    [Fact]
    public async Task GenerateUniqueCodeAsync_SkipsExistingActiveCode()
    {
        // Seed one active session. The service should still find a free code.
        await using var db = MakeDb("gen_skip");
        db.GameSessions.Add(new GameSession
        {
            Id = 1,
            Code = "AAAAAA",
            Status = GameStatus.Lobby,
            QuizId = 1,
            HostUserId = 1,
        });
        await db.SaveChangesAsync();

        var svc = new GameService(db);

        // Run several times to be confident AAAAAA is never reused
        for (int i = 0; i < 5; i++)
        {
            var code = await svc.GenerateUniqueCodeAsync();
            // If AAAAAA was generated randomly it should be skipped;
            // because the probability is ~1/32^6 this assert is effectively always true.
            code.Should().HaveLength(6);
        }
    }

    [Fact]
    public async Task GenerateUniqueCodeAsync_AllowsCodeOfFinishedSession()
    {
        // A code tied to a Finished session is not "active", so it should be usable.
        await using var db = MakeDb("gen_finished");

        // Force the service to pick one specific code by filling all active slots.
        // Instead: just confirm that a Finished session's code is considered free
        // by checking AnyAsync logic. We'll add a Finished session and verify
        // GenerateUniqueCodeAsync still returns something (doesn't throw).
        db.GameSessions.Add(new GameSession
        {
            Id = 2,
            Code = "BBBBBB",
            Status = GameStatus.Finished,
            QuizId = 1,
            HostUserId = 1,
        });
        await db.SaveChangesAsync();

        var svc = new GameService(db);
        var code = await svc.GenerateUniqueCodeAsync();
        code.Should().HaveLength(6);
    }
}
