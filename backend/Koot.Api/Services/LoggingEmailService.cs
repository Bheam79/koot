namespace Koot.Api.Services;

/// <summary>
/// Default <see cref="IEmailService"/> implementation used in dev/staging. Does not
/// send any real email; logs the recipient, subject, and body at Information level
/// so devs can copy values (e.g. password reset tokens) directly from the log stream.
/// </summary>
public class LoggingEmailService : IEmailService
{
    private readonly ILogger<LoggingEmailService> _logger;

    public LoggingEmailService(ILogger<LoggingEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string to, string subject, string body)
    {
        _logger.LogInformation(
            "[Email] To: {To} | Subject: {Subject} | Body: {Body}",
            to, subject, body);
        return Task.CompletedTask;
    }
}
