using System.Net;
using System.Net.Mail;
using Backend.Options;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var from = string.IsNullOrWhiteSpace(settings.FromEmail) ? settings.SmtpUsername : settings.FromEmail;
        if (string.IsNullOrWhiteSpace(settings.SmtpHost) || string.IsNullOrWhiteSpace(from))
        {
            logger.LogWarning("SMTP is enabled but host/from is missing. Email was not sent.");
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(from, string.IsNullOrWhiteSpace(settings.FromName) ? "Car Rental" : settings.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(settings.SmtpUsername, settings.SmtpPassword)
        };

        try
        {
            await client.SendMailAsync(message, cancellationToken);
            logger.LogInformation("Sent email '{Subject}'.", subject);
        }
        catch (SmtpException ex)
        {
            logger.LogError(ex, "SMTP send failed for '{Subject}'.", subject);
            throw;
        }
    }
}
