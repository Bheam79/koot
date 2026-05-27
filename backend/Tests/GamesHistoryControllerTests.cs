using System.Security.Claims;
using FluentAssertions;
using Koot.Api.Controllers;
using Koot.Api.Data;
using Koot.Api.Dtos.Games;
using Koot.Api.Models;
using Koot.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace KootTests;

/// <summary>
/// Direct controller-level tests for the host-only game history & analytics
/// endpoints added in KOOT-28:
///   GET /api/games/history
///   GET /api/games/sessions/{id}
///   GET /api/games/sessions/{id}/answers
///
/// Uses the EF Core in-memory provider (same as the other *ControllerTests).
/// GameService / GameStateService are passed as null because none of the
/// endpoints under test touch them; if a future endpoint does, this would
/// throw NRE and the test would force a refactor here.
/// </summary>
public class GamesHistoryControllerTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static AppDbContext MakeDb(string name)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(opts);
    }

    /// <summary>
    /// Build a controller bound to <paramref name="db"/>. If <paramref name="userId"/>
    /// is null the principal has no auth claims, simulating an unauthenticated
    /// caller (the [Authorize] attribute itself can't be exercised here — the
    /// action method returns 401 via CurrentUser.TryGetId when no id claim is
    /// present, which is equivalent behaviour for our purposes).
    /// </summary>
    private static GamesController MakeController(AppDbContext db, int? userId)
    {
        // GameService / GameStateService are not exercised by the history
        // endpoints, so a null reference is acceptable. See xml doc above.
        var ctrl = new GamesController(
            db,
            gameService: null!,
            stateService: null!,
            logger: NullLogger<GamesController>.Instance);

        var identity = userId.HasValue
            ? new ClaimsIdentity(new[]
            {
                new Claim("userId", userId.Value.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()),
            }, authenticationType: "Test")
            : new ClaimsIdentity(); // unauthenticated

        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
            },
        };

        return ctrl;
    }

    // ── Seed data ───────────────────────────────────────────────────────────
    //
    // World:
    //   user 1 (alice)  hosts quiz 1 (Alice Quiz) — sessions 1 (Finished)
    //                                              & 2 (Finished)
    //                   hosts quiz 2 (Alice Quiz 2) — session 3 (Finished)
    //                   hosts session 4 (Lobby, not finished — should not show)
    //   user 2 (bob)    hosts quiz 3 — session 5 (Finished)
    //
    // Session 1 is fully populated with questions, options, participants and
    // answers so the per-question analytics endpoints have something to chew on.
    private static async Task<AppDbContext> SeedAsync(string name)
    {
        var db = MakeDb(name);

        // Users
        db.Users.Add(new User { Id = 1, Username = "alice", Email = "a@e.com", PasswordHash = "h" });
        db.Users.Add(new User { Id = 2, Username = "bob",   Email = "b@e.com", PasswordHash = "h" });

        // Quizzes
        var q1 = new Quiz { Id = 1, UserId = 1, Title = "Alice Quiz" };
        var q2 = new Quiz { Id = 2, UserId = 1, Title = "Alice Quiz 2" };
        var q3 = new Quiz { Id = 3, UserId = 2, Title = "Bob Quiz" };
        db.Quizzes.AddRange(q1, q2, q3);

        // Questions for quiz 1 — used by detailed session 1.
        // Q1 is multiple-choice (4 options, opt A correct).
        // Q2 is type-answer (correct = "Paris").
        var qn1 = new Question
        {
            Id = 101, QuizId = 1, OrderIndex = 0,
            Type = QuestionType.MultipleChoice, QuestionText = "MC question",
            TimeLimit = 20, Points = 1000,
        };
        var qn2 = new Question
        {
            Id = 102, QuizId = 1, OrderIndex = 1,
            Type = QuestionType.TypeAnswer, QuestionText = "Capital of France?",
            TimeLimit = 20, Points = 1000,
        };
        db.Questions.AddRange(qn1, qn2);

        // MC options for qn1
        db.AnswerOptions.AddRange(
            new AnswerOption { Id = 1001, QuestionId = 101, Text = "A", IsCorrect = true,  OrderIndex = 0 },
            new AnswerOption { Id = 1002, QuestionId = 101, Text = "B", IsCorrect = false, OrderIndex = 1 },
            new AnswerOption { Id = 1003, QuestionId = 101, Text = "C", IsCorrect = false, OrderIndex = 2 },
            new AnswerOption { Id = 1004, QuestionId = 101, Text = "D", IsCorrect = false, OrderIndex = 3 });

        // TypeAnswer correct text option for qn2
        db.AnswerOptions.Add(new AnswerOption
        {
            Id = 1005, QuestionId = 102, Text = "Paris", IsCorrect = true, OrderIndex = 0,
        });

        // Sessions — all timestamps deterministic for sorting.
        var t0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        db.GameSessions.AddRange(
            new GameSession
            {
                Id = 1, QuizId = 1, HostUserId = 1, Code = "ALICE1",
                Status = GameStatus.Finished,
                CreatedAt = t0, StartedAt = t0.AddMinutes(1), EndedAt = t0.AddMinutes(11),
            },
            new GameSession
            {
                Id = 2, QuizId = 1, HostUserId = 1, Code = "ALICE2",
                Status = GameStatus.Finished,
                CreatedAt = t0.AddHours(1), StartedAt = t0.AddHours(1).AddMinutes(1),
                EndedAt = t0.AddHours(1).AddMinutes(6),
            },
            new GameSession
            {
                Id = 3, QuizId = 2, HostUserId = 1, Code = "ALICE3",
                Status = GameStatus.Finished,
                CreatedAt = t0.AddHours(2), StartedAt = t0.AddHours(2).AddMinutes(1),
                EndedAt = t0.AddHours(2).AddMinutes(8),
            },
            new GameSession
            {
                Id = 4, QuizId = 1, HostUserId = 1, Code = "ALICE4",
                Status = GameStatus.Lobby, // not finished — should NOT appear in history
                CreatedAt = t0.AddHours(3),
            },
            new GameSession
            {
                Id = 5, QuizId = 3, HostUserId = 2, Code = "BOBSES",
                Status = GameStatus.Finished,
                CreatedAt = t0, StartedAt = t0.AddMinutes(1), EndedAt = t0.AddMinutes(5),
            });

        // Participants for session 1 (4 players with varying scores)
        db.GameParticipants.AddRange(
            new GameParticipant { Id = 10, SessionId = 1, Nickname = "p1", AvatarId = 1, TotalScore = 1000 },
            new GameParticipant { Id = 11, SessionId = 1, Nickname = "p2", AvatarId = 2, TotalScore = 500 },
            new GameParticipant { Id = 12, SessionId = 1, Nickname = "p3", AvatarId = 3, TotalScore = 0 },
            new GameParticipant { Id = 13, SessionId = 1, Nickname = "p4", AvatarId = 4, TotalScore = 100 });

        // Participants for session 2 (1 player)
        db.GameParticipants.Add(new GameParticipant
        {
            Id = 20, SessionId = 2, Nickname = "solo", AvatarId = 1, TotalScore = 300,
        });

        // Participants for session 5 (bob's) — irrelevant to alice's tests
        db.GameParticipants.Add(new GameParticipant
        {
            Id = 30, SessionId = 5, Nickname = "bobby", AvatarId = 1, TotalScore = 999,
        });

        // Answers for session 1 question 1 (the MC question)
        // - p1 → A (correct)
        // - p2 → A (correct)
        // - p3 → B (wrong)
        // - p4 → C (wrong)
        // => 2/4 correct = 50% accuracy. A has 2 picks, B has 1, C has 1, D has 0.
        var answeredAt = t0.AddMinutes(2);
        db.GameAnswers.AddRange(
            new GameAnswer
            {
                Id = 5001, SessionId = 1, ParticipantId = 10, QuestionId = 101,
                AnswerOptionId = 1001, IsCorrect = true, PointsEarned = 1000,
                TimeTakenMs = 2000, AnsweredAt = answeredAt,
            },
            new GameAnswer
            {
                Id = 5002, SessionId = 1, ParticipantId = 11, QuestionId = 101,
                AnswerOptionId = 1001, IsCorrect = true, PointsEarned = 500,
                TimeTakenMs = 4000, AnsweredAt = answeredAt.AddSeconds(1),
            },
            new GameAnswer
            {
                Id = 5003, SessionId = 1, ParticipantId = 12, QuestionId = 101,
                AnswerOptionId = 1002, IsCorrect = false, PointsEarned = 0,
                TimeTakenMs = 6000, AnsweredAt = answeredAt.AddSeconds(2),
            },
            new GameAnswer
            {
                Id = 5004, SessionId = 1, ParticipantId = 13, QuestionId = 101,
                AnswerOptionId = 1003, IsCorrect = false, PointsEarned = 0,
                TimeTakenMs = 8000, AnsweredAt = answeredAt.AddSeconds(3),
            });

        // Answers for session 1 question 2 (TypeAnswer)
        // - p1 → "Paris" correct
        // - p2 → "London" wrong
        db.GameAnswers.AddRange(
            new GameAnswer
            {
                Id = 5005, SessionId = 1, ParticipantId = 10, QuestionId = 102,
                AnswerText = "Paris", IsCorrect = true, PointsEarned = 1000,
                TimeTakenMs = 3000, AnsweredAt = answeredAt.AddMinutes(1),
            },
            new GameAnswer
            {
                Id = 5006, SessionId = 1, ParticipantId = 11, QuestionId = 102,
                AnswerText = "London", IsCorrect = false, PointsEarned = 0,
                TimeTakenMs = 5000, AnsweredAt = answeredAt.AddMinutes(1).AddSeconds(1),
            });

        await db.SaveChangesAsync();
        return db;
    }

    // ─── GET /api/games/history ──────────────────────────────────────────────

    [Fact]
    public async Task History_ReturnsUnauthorized_WhenNoUserClaim()
    {
        await using var db = await SeedAsync("history_unauth");
        var ctrl = MakeController(db, userId: null);

        var result = await ctrl.GetHistory(quizId: null);

        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task History_ReturnsEmpty_WhenUserHasNoFinishedSessions()
    {
        await using var db = await SeedAsync("history_empty");
        // user 3 doesn't exist in seed data — they certainly have no sessions.
        var ctrl = MakeController(db, userId: 3);

        var result = await ctrl.GetHistory(quizId: null);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var page = ok.Value.Should().BeOfType<PagedResultDto<GameHistorySummaryDto>>().Subject;
        page.Total.Should().Be(0);
        page.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task History_ReturnsAliceSessions_WithCorrectAggregates()
    {
        await using var db = await SeedAsync("history_alice");
        var ctrl = MakeController(db, userId: 1);

        var result = await ctrl.GetHistory(quizId: null);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var page = ok.Value.Should().BeOfType<PagedResultDto<GameHistorySummaryDto>>().Subject;

        // Three Finished sessions: 1, 2, 3 (session 4 is Lobby; session 5 belongs to bob)
        page.Total.Should().Be(3);
        page.Items.Should().HaveCount(3);

        // Sorted by EndedAt DESC: session 3, 2, 1
        page.Items[0].Id.Should().Be(3);
        page.Items[1].Id.Should().Be(2);
        page.Items[2].Id.Should().Be(1);

        // Session 1 had 4 participants with scores 1000, 500, 0, 100 → avg 400
        var s1 = page.Items.Single(x => x.Id == 1);
        s1.ParticipantCount.Should().Be(4);
        s1.AverageScore.Should().Be(400.0);
        s1.TopScorerNickname.Should().Be("p1");
        s1.TopScorerScore.Should().Be(1000);
        s1.DurationSeconds.Should().Be(10 * 60); // 10 min between StartedAt and EndedAt
        s1.QuizTitle.Should().Be("Alice Quiz");

        // Session 2 had 1 participant ("solo", score 300)
        var s2 = page.Items.Single(x => x.Id == 2);
        s2.ParticipantCount.Should().Be(1);
        s2.AverageScore.Should().Be(300.0);
        s2.TopScorerNickname.Should().Be("solo");
    }

    [Fact]
    public async Task History_FilterByQuizId_OnlyReturnsThatQuizSessions()
    {
        await using var db = await SeedAsync("history_filter");
        var ctrl = MakeController(db, userId: 1);

        var result = await ctrl.GetHistory(quizId: 1);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var page = ok.Value.Should().BeOfType<PagedResultDto<GameHistorySummaryDto>>().Subject;

        // Only sessions 1 and 2 belong to quiz 1 (and are Finished).
        page.Total.Should().Be(2);
        page.Items.Should().OnlyContain(s => s.QuizId == 1);
        page.Items.Select(s => s.Id).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public async Task History_DoesNotLeakOtherHostsSessions()
    {
        await using var db = await SeedAsync("history_cross_host");
        var ctrl = MakeController(db, userId: 2); // bob

        var result = await ctrl.GetHistory(quizId: null);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var page = ok.Value.Should().BeOfType<PagedResultDto<GameHistorySummaryDto>>().Subject;

        // Bob owns only session 5; nothing belonging to alice should appear.
        page.Total.Should().Be(1);
        page.Items.Should().ContainSingle().Which.Id.Should().Be(5);
    }

    // ─── GET /api/games/sessions/{id} ────────────────────────────────────────

    [Fact]
    public async Task SessionDetail_ReturnsNotFound_ForMissingId()
    {
        await using var db = await SeedAsync("detail_notfound");
        var ctrl = MakeController(db, userId: 1);

        var result = await ctrl.GetSessionDetail(9999);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SessionDetail_ReturnsForbid_WhenUserIsNotHost()
    {
        await using var db = await SeedAsync("detail_forbid");
        // Alice (1) tries to read bob's session 5
        var ctrl = MakeController(db, userId: 1);

        var result = await ctrl.GetSessionDetail(5);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task SessionDetail_ReturnsStandings_OrderedByScoreDesc()
    {
        await using var db = await SeedAsync("detail_standings");
        var ctrl = MakeController(db, userId: 1);

        var result = await ctrl.GetSessionDetail(1);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var detail = ok.Value.Should().BeOfType<SessionDetailDto>().Subject;

        // Expected order by TotalScore DESC: p1 (1000), p2 (500), p4 (100), p3 (0).
        detail.Standings.Select(s => s.Nickname)
            .Should().Equal("p1", "p2", "p4", "p3");

        detail.Standings.Select(s => s.Rank)
            .Should().Equal(1, 2, 3, 4);

        // CorrectCount on p1 = 2 (both questions); p2 = 1; p3 = 0; p4 = 0.
        var byName = detail.Standings.ToDictionary(s => s.Nickname, s => s);
        byName["p1"].CorrectCount.Should().Be(2);
        byName["p2"].CorrectCount.Should().Be(1);
        byName["p3"].CorrectCount.Should().Be(0);
        byName["p4"].CorrectCount.Should().Be(0);

        byName["p1"].TotalQuestions.Should().Be(2);
    }

    [Fact]
    public async Task SessionDetail_ComputesPerQuestionAccuracy()
    {
        await using var db = await SeedAsync("detail_accuracy");
        var ctrl = MakeController(db, userId: 1);

        var result = await ctrl.GetSessionDetail(1);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var detail = ok.Value.Should().BeOfType<SessionDetailDto>().Subject;

        // Q1 (MC): 4 answers, 2 correct → 50%
        var q1Stat = detail.QuestionStats.Single(q => q.QuestionId == 101);
        q1Stat.AnswerCount.Should().Be(4);
        q1Stat.CorrectCount.Should().Be(2);
        q1Stat.AccuracyPct.Should().Be(50.0);

        // Q2 (TypeAnswer): 2 answers, 1 correct → 50%
        var q2Stat = detail.QuestionStats.Single(q => q.QuestionId == 102);
        q2Stat.AnswerCount.Should().Be(2);
        q2Stat.CorrectCount.Should().Be(1);
        q2Stat.AccuracyPct.Should().Be(50.0);

        // TypeAnswer question exposes the correct text(s), not an option distribution.
        q2Stat.CorrectAnswerTexts.Should().ContainSingle().Which.Should().Be("Paris");
        q2Stat.OptionDistribution.Should().BeEmpty();
    }

    [Fact]
    public async Task SessionDetail_ReturnsOptionDistribution_ForMcQuestion()
    {
        await using var db = await SeedAsync("detail_options");
        var ctrl = MakeController(db, userId: 1);

        var result = await ctrl.GetSessionDetail(1);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var detail = ok.Value.Should().BeOfType<SessionDetailDto>().Subject;

        var q1Stat = detail.QuestionStats.Single(q => q.QuestionId == 101);

        // Distribution should list all 4 options in OrderIndex order with the
        // correct pick counts (A:2, B:1, C:1, D:0).
        q1Stat.OptionDistribution.Should().HaveCount(4);
        var byText = q1Stat.OptionDistribution.ToDictionary(o => o.OptionText, o => o);
        byText["A"].PickCount.Should().Be(2);
        byText["B"].PickCount.Should().Be(1);
        byText["C"].PickCount.Should().Be(1);
        byText["D"].PickCount.Should().Be(0);

        // Percentages should add up to 100 (within rounding tolerance).
        byText["A"].PickPct.Should().Be(50.0);
        byText["B"].PickPct.Should().Be(25.0);
        byText["C"].PickPct.Should().Be(25.0);
        byText["D"].PickPct.Should().Be(0.0);

        // Q1 is MC, so it should NOT carry CorrectAnswerTexts.
        q1Stat.CorrectAnswerTexts.Should().BeEmpty();
    }

    // ─── GET /api/games/sessions/{id}/answers ────────────────────────────────

    [Fact]
    public async Task SessionAnswers_ReturnsNotFound_ForMissingId()
    {
        await using var db = await SeedAsync("answers_notfound");
        var ctrl = MakeController(db, userId: 1);

        var result = await ctrl.GetSessionAnswers(9999);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SessionAnswers_ReturnsForbid_WhenUserIsNotHost()
    {
        await using var db = await SeedAsync("answers_forbid");
        var ctrl = MakeController(db, userId: 1); // alice reading bob's session 5

        var result = await ctrl.GetSessionAnswers(5);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task SessionAnswers_ReturnsOneRowPerAnswer_WithJoinedFields()
    {
        await using var db = await SeedAsync("answers_rows");
        var ctrl = MakeController(db, userId: 1);

        var result = await ctrl.GetSessionAnswers(1);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var rows = ok.Value.Should().BeAssignableTo<IEnumerable<SessionAnswerRowDto>>().Subject.ToList();

        // 6 answers were seeded for session 1 (4 on q1, 2 on q2).
        rows.Should().HaveCount(6);

        // Pick the row for p1 / q1 (MC, correct option A)
        var p1Q1 = rows.Single(r =>
            r.ParticipantNickname == "p1" && r.QuestionText == "MC question");
        p1Q1.SelectedOptionText.Should().Be("A");
        p1Q1.AnswerText.Should().BeNull();
        p1Q1.IsCorrect.Should().BeTrue();
        p1Q1.PointsEarned.Should().Be(1000);
        p1Q1.QuestionType.Should().Be(QuestionType.MultipleChoice);

        // Pick p2's row for q2 (TypeAnswer, "London", wrong)
        var p2Q2 = rows.Single(r =>
            r.ParticipantNickname == "p2" && r.QuestionText == "Capital of France?");
        p2Q2.AnswerText.Should().Be("London");
        p2Q2.SelectedOptionText.Should().BeNull();
        p2Q2.IsCorrect.Should().BeFalse();
        p2Q2.QuestionType.Should().Be(QuestionType.TypeAnswer);
    }
}
