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
[Route("api/quizzes/{quizId:int}/questions")]
public class QuestionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<QuestionsController> _logger;

    public QuestionsController(AppDbContext db, ILogger<QuestionsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Add a question to a quiz.</summary>
    [HttpPost]
    public async Task<ActionResult<QuestionDto>> Create(int quizId, [FromBody] CreateQuestionRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        if (!CurrentUser.TryGetId(User, out var userId))
        {
            return Unauthorized();
        }

        var quiz = await _db.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId);
        if (quiz is null)
        {
            return NotFound(new { error = "Quiz not found." });
        }
        if (quiz.UserId != userId)
        {
            return Forbid();
        }

        var validation = ValidateQuestionPayload(
            request.Type,
            request.AnswerOptions);
        if (validation is not null)
        {
            return validation;
        }

        // Determine next OrderIndex if caller didn't supply one.
        int orderIndex;
        if (request.OrderIndex.HasValue)
        {
            orderIndex = request.OrderIndex.Value;
        }
        else
        {
            var maxOrder = await _db.Questions
                .Where(q => q.QuizId == quizId)
                .Select(q => (int?)q.OrderIndex)
                .MaxAsync() ?? -1;
            orderIndex = maxOrder + 1;
        }

        var question = new Question
        {
            QuizId = quizId,
            Type = request.Type,
            QuestionText = request.QuestionText.Trim(),
            TimeLimit = request.TimeLimit,
            Points = request.Points,
            ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
            OrderIndex = orderIndex,
            AnswerOptions = BuildAnswerOptions(request.Type, request.AnswerOptions),
        };

        _db.Questions.Add(question);
        quiz.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Created question {QuestionId} on quiz {QuizId} for user {UserId}",
            question.Id, quizId, userId);

        return CreatedAtAction(
            nameof(Create),
            new { quizId = quizId, questionId = question.Id },
            QuizzesController.MapQuestion(question));
    }

    /// <summary>Replace an existing question's content and answer options.</summary>
    [HttpPut("{questionId:int}")]
    public async Task<ActionResult<QuestionDto>> Update(int quizId, int questionId, [FromBody] UpdateQuestionRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        if (!CurrentUser.TryGetId(User, out var userId))
        {
            return Unauthorized();
        }

        var question = await _db.Questions
            .Include(q => q.AnswerOptions)
            .Include(q => q.Quiz)
            .FirstOrDefaultAsync(q => q.Id == questionId && q.QuizId == quizId);

        if (question is null || question.Quiz is null)
        {
            return NotFound();
        }
        if (question.Quiz.UserId != userId)
        {
            return Forbid();
        }

        var validation = ValidateQuestionPayload(request.Type, request.AnswerOptions);
        if (validation is not null)
        {
            return validation;
        }

        question.Type = request.Type;
        question.QuestionText = request.QuestionText.Trim();
        question.TimeLimit = request.TimeLimit;
        question.Points = request.Points;
        question.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
        if (request.OrderIndex.HasValue)
        {
            question.OrderIndex = request.OrderIndex.Value;
        }

        // Replace answer options wholesale - simplest correct semantics.
        _db.AnswerOptions.RemoveRange(question.AnswerOptions);
        question.AnswerOptions = BuildAnswerOptions(request.Type, request.AnswerOptions);

        question.Quiz.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(QuizzesController.MapQuestion(question));
    }

    /// <summary>Delete a question (and its answer options).</summary>
    [HttpDelete("{questionId:int}")]
    public async Task<IActionResult> Delete(int quizId, int questionId)
    {
        if (!CurrentUser.TryGetId(User, out var userId))
        {
            return Unauthorized();
        }

        var question = await _db.Questions
            .Include(q => q.Quiz)
            .FirstOrDefaultAsync(q => q.Id == questionId && q.QuizId == quizId);

        if (question is null || question.Quiz is null)
        {
            return NotFound();
        }
        if (question.Quiz.UserId != userId)
        {
            return Forbid();
        }

        _db.Questions.Remove(question);
        question.Quiz.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>Reorder all questions in a quiz. Accepts [{id, orderIndex}, ...].</summary>
    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder(int quizId, [FromBody] ReorderQuestionsRequest request)
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
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.Id == quizId);

        if (quiz is null)
        {
            return NotFound();
        }
        if (quiz.UserId != userId)
        {
            return Forbid();
        }

        if (request.Items.Count == 0)
        {
            return NoContent();
        }

        var byId = quiz.Questions.ToDictionary(q => q.Id);
        foreach (var item in request.Items)
        {
            if (!byId.TryGetValue(item.Id, out var question))
            {
                return BadRequest(new { error = $"Question {item.Id} does not belong to quiz {quizId}." });
            }
            question.OrderIndex = item.OrderIndex;
        }

        quiz.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // ---------- helpers ----------

    /// <summary>
    /// Validate the supplied answer options against the question type. Returns null on
    /// success; otherwise an ActionResult (BadRequest) describing the failure.
    /// </summary>
    private ActionResult? ValidateQuestionPayload(QuestionType type, List<CreateAnswerOptionRequest> options)
    {
        switch (type)
        {
            case QuestionType.MultipleChoice:
                if (options.Count < 2 || options.Count > 4)
                {
                    return BadRequest(new { error = "Multiple choice questions require 2-4 options." });
                }
                if (!options.Any(o => o.IsCorrect))
                {
                    return BadRequest(new { error = "Multiple choice questions require at least one correct option." });
                }
                if (options.Any(o => string.IsNullOrWhiteSpace(o.Text)))
                {
                    return BadRequest(new { error = "Multiple choice option text cannot be empty." });
                }
                break;

            case QuestionType.TrueFalse:
                // Options are ignored on input - we always synthesize them.
                break;

            case QuestionType.TypeAnswer:
                if (options.Count < 1 || string.IsNullOrWhiteSpace(options[0].Text))
                {
                    return BadRequest(new { error = "TypeAnswer questions require a correct answer text." });
                }
                break;

            case QuestionType.Poll:
                if (options.Count < 2 || options.Count > 4)
                {
                    return BadRequest(new { error = "Poll questions require 2-4 options." });
                }
                if (options.Any(o => string.IsNullOrWhiteSpace(o.Text)))
                {
                    return BadRequest(new { error = "Poll option text cannot be empty." });
                }
                break;

            default:
                return BadRequest(new { error = $"Unknown question type: {type}." });
        }

        return null;
    }

    /// <summary>
    /// Materialise the AnswerOption rows that should be persisted for a question of the
    /// given type and supplied input. Trusts that ValidateQuestionPayload was already
    /// called.
    /// </summary>
    private static List<AnswerOption> BuildAnswerOptions(QuestionType type, List<CreateAnswerOptionRequest> options)
    {
        switch (type)
        {
            case QuestionType.MultipleChoice:
                return options
                    .Select((o, i) => new AnswerOption
                    {
                        Text = o.Text.Trim(),
                        IsCorrect = o.IsCorrect,
                        OrderIndex = o.OrderIndex != 0 ? o.OrderIndex : i,
                    })
                    .ToList();

            case QuestionType.TrueFalse:
                {
                    // Caller may indicate which one is correct via the first supplied option.
                    var trueIsCorrect = true;
                    if (options.Count >= 1)
                    {
                        var first = options[0];
                        if (first.Text.Trim().Equals("False", StringComparison.OrdinalIgnoreCase))
                        {
                            trueIsCorrect = !first.IsCorrect;
                        }
                        else if (first.Text.Trim().Equals("True", StringComparison.OrdinalIgnoreCase))
                        {
                            trueIsCorrect = first.IsCorrect;
                        }
                    }
                    return new List<AnswerOption>
                    {
                        new() { Text = "True",  IsCorrect = trueIsCorrect,  OrderIndex = 0 },
                        new() { Text = "False", IsCorrect = !trueIsCorrect, OrderIndex = 1 },
                    };
                }

            case QuestionType.TypeAnswer:
                return new List<AnswerOption>
                {
                    new()
                    {
                        Text = options[0].Text.Trim(),
                        IsCorrect = true,
                        OrderIndex = 0,
                    },
                };

            case QuestionType.Poll:
                return options
                    .Select((o, i) => new AnswerOption
                    {
                        Text = o.Text.Trim(),
                        IsCorrect = false, // polls have no correct answer
                        OrderIndex = o.OrderIndex != 0 ? o.OrderIndex : i,
                    })
                    .ToList();

            default:
                return new List<AnswerOption>();
        }
    }
}
