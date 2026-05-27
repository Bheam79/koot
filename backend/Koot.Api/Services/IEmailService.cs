namespace Koot.Api.Services;

/// <summary>
/// Abstraction for sending transactional email. Implementations may dispatch to
/// SMTP, a third-party provider, or — in dev — just log the message.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Send an email. Implementations are expected to be best-effort; callers
    /// should not block business flows on delivery failure.
    /// </summary>
    Task SendAsync(string to, string subject, string body);
}
