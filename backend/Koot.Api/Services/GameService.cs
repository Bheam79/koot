using Koot.Api.Data;
using Koot.Api.Dtos.Games;
using Koot.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Koot.Api.Services;

/// <summary>
/// Scoped service for database-backed game operations.
/// </summary>
public class GameService
{
    private static readonly char[] CodeChars =
        "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray(); // omit ambiguous I/O/1/0

    private readonly AppDbContext _db;

    public GameService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Generates a unique 6-character alphanumeric code not already used by an
    /// active (non-Finished) session.
    /// </summary>
    public async Task<string> GenerateUniqueCodeAsync(CancellationToken ct = default)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            var code = GenerateCode();
            bool taken = await _db.GameSessions
                .AnyAsync(s => s.Code == code && s.Status != GameStatus.Finished, ct);
            if (!taken) return code;
        }

        throw new InvalidOperationException("Could not generate a unique game code after 20 attempts.");
    }

    private static string GenerateCode()
    {
        Span<char> buf = stackalloc char[6];
        for (int i = 0; i < 6; i++)
            buf[i] = CodeChars[Random.Shared.Next(CodeChars.Length)];
        return new string(buf);
    }

    /// <summary>
    /// Calculates score for a question answer.
    /// Correct: points * (1 - timeTaken / (timeLimit * 1000) * 0.5).
    /// Wrong / Poll: 0.
    /// </summary>
    public static int CalculateScore(bool isCorrect, int timeTakenMs, int timeLimit, int maxPoints)
    {
        if (!isCorrect) return 0;
        var timeFraction = Math.Clamp((double)timeTakenMs / ((double)timeLimit * 1000), 0, 1);
        return (int)Math.Round(maxPoints * (1 - timeFraction * 0.5));
    }

    /// <summary>
    /// Strips IsCorrect from all answer options so the payload is safe to send to players.
    /// </summary>
    public static QuestionBroadcastDto GetQuestionForBroadcast(Question question)
    {
        return new QuestionBroadcastDto
        {
            Id = question.Id,
            Type = (int)question.Type,
            QuestionText = question.QuestionText,
            TimeLimit = question.TimeLimit,
            Points = question.Points,
            ImageUrl = question.ImageUrl,
            OrderIndex = question.OrderIndex,
            AnswerOptions = question.AnswerOptions
                .OrderBy(o => o.OrderIndex)
                .Select(o => new AnswerOptionBroadcastDto
                {
                    Id = o.Id,
                    Text = o.Text,
                    OrderIndex = o.OrderIndex,
                })
                .ToList(),
        };
    }
}
