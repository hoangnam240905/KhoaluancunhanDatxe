using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Backend.Options;

namespace Backend.Services;

/// <summary>
/// Used when SMTP is disabled or not configured. Does not log message bodies (OTP).
/// </summary>
public sealed class NullEmailSender(ILogger<NullEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Email sending is disabled. Message '{Subject}' was not delivered.", subject);
        return Task.CompletedTask;
    }
}
