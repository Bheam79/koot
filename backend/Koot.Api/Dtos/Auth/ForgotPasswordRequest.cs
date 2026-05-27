using System.ComponentModel.DataAnnotations;

namespace Koot.Api.Dtos.Auth;

public class ForgotPasswordRequest
{
    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = string.Empty;
}
