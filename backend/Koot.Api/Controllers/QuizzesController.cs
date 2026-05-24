using Koot.Api.Data;
using Koot.Api.Dtos.Quizzes;
using Koot.Api.Models;
using Koot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Koot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/quizzes")]
public class QuizzesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<QuizzesController> _logger;

    public QuizzesController(AppDbContext db, ILogger<QuizzesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>List quizzes owned by the current user.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<QuizSummaryDto>>> List()
    {
        if (!CurrentUser.TryGetId(User, out var userId))
        {
            return Unauthorized();
        }

        var quizzes = await _db.Quizzes
            .Where(q => q.UserId == userId)
            .OrderByDescending(q => q.CreatedAt)
            .Select(q => new QuizSummaryDto
            {
                Id = q.Id,
                Title = q.Title,
                Description = q.Description,
                CoverImageUrl = q.CoverImageUrl,
                QuestionCount = q.Questions.Count,
                CreatedAt = q.CreatedAt,
            })
            .ToListAsync();

        return Ok(quizzes);
    }

    /// <summary>Get a single quiz (must be owned by the current user) with all questions and options.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<QuizDetailDto>> Get(int id)
    {
        if (!CurrentUser.TryGetId(User, out var userId))
        {
            return Unauthorized();
        }

        var quiz = await _db.Quizzes
            .AsNoTracking()
            .Include(q => q.Questions.OrderBy(qu => qu.OrderIndex))
                .ThenInclude(qu => qu.AnswerOptions.OrderBy(o => o.OrderIndex))
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quiz is null)
        {
            return NotFound();
        }

        if (quiz.UserId != userId)
        {
            return Forbid();
        }

        return Ok(MapQuiz(quiz));
    }

    /// <summary>Create a new quiz owned by the current user.</summary>
    [HttpPost]
    public async Task<ActionResult<QuizDetailDto>> Create([FromBody] CreateQuizRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        if (!CurrentUser.TryGetId(User, out var userId))
        {
            return Unauthorized();
        }

        var now = DateTime.UtcNow;
        var quiz = new Quiz
        {
            UserId = userId,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CoverImageUrl = string.IsNullOrWhiteSpace(request.CoverImageUrl) ? null : request.CoverImageUrl.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Quizzes.Add(quiz);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created quiz {QuizId} for user {UserId}", quiz.Id, userId);

        return CreatedAtAction(nameof(Get), new { id = quiz.Id }, MapQuiz(quiz));
    }

    /// <summary>Update quiz metadata.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<QuizDetailDto>> Update(int id, [FromBody] UpdateQuizRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        if (!CurrentUser.TryGetId(User, out var userId))
        {
            return Unauthorized();
        }

        var quiz = await _db.Quizzes
            .Include(q => q.Questions.OrderBy(qu => qu.OrderIndex))
                .ThenInclude(qu => qu.AnswerOptions.OrderBy(o => o.OrderIndex))
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quiz is null)
        {
            return NotFound();
        }

        if (quiz.UserId != userId)
        {
            return Forbid();
        }

        quiz.Title = request.Title.Trim();
        quiz.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        quiz.CoverImageUrl = string.IsNullOrWhiteSpace(request.CoverImageUrl) ? null : request.CoverImageUrl.Trim();
        quiz.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(MapQuiz(quiz));
    }

    /// <summary>Delete a quiz. Cascades to questions and answer options.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!CurrentUser.TryGetId(User, out var userId))
        {
            return Unauthorized();
        }

        var quiz = await _db.Quizzes.FirstOrDefaultAsync(q => q.Id == id);
        if (quiz is null)
        {
            return NotFound();
        }

        if (quiz.UserId != userId)
        {
            return Forbid();
        }

        _db.Quizzes.Remove(quiz);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Deleted quiz {QuizId} for user {UserId}", id, userId);
        return NoContent();
    }

    internal static QuizDetailDto MapQuiz(Quiz quiz) => new()
    {
        Id = quiz.Id,
        Title = quiz.Title,
        Description = quiz.Description,
        CoverImageUrl = quiz.CoverImageUrl,
        CreatedAt = quiz.CreatedAt,
        UpdatedAt = quiz.UpdatedAt,
        Questions = quiz.Questions
            .OrderBy(q => q.OrderIndex)
            .Select(MapQuestion)
            .ToList(),
    };

    internal static QuestionDto MapQuestion(Question q) => new()
    {
        Id = q.Id,
        Type = q.Type,
        QuestionText = q.QuestionText,
        TimeLimit = q.TimeLimit,
        Points = q.Points,
        ImageUrl = q.ImageUrl,
        OrderIndex = q.OrderIndex,
        AnswerOptions = q.AnswerOptions
            .OrderBy(o => o.OrderIndex)
            .Select(o => new AnswerOptionDto
            {
                Id = o.Id,
                Text = o.Text,
                IsCorrect = o.IsCorrect,
                OrderIndex = o.OrderIndex,
            })
            .ToList(),
    };
}
