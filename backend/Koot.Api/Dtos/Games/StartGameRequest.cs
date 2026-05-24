using System.ComponentModel.DataAnnotations;

namespace Koot.Api.Dtos.Games;

public class StartGameRequest
{
    [Required]
    public int QuizId { get; set; }
}
