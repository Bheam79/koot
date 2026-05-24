using System.ComponentModel.DataAnnotations;

namespace Koot.Api.Dtos.Quizzes;

public class ReorderQuestionItem
{
    [Required]
    public int Id { get; set; }

    [Required]
    public int OrderIndex { get; set; }
}

public class ReorderQuestionsRequest
{
    [Required]
    public List<ReorderQuestionItem> Items { get; set; } = new();
}
