using System.Text.RegularExpressions;
using Backend.Services;

namespace Backend.Tests;

public sealed class CapturingEmailSender : IEmailSender
{
    private static readonly Regex OtpRegex = new(@"\b(\d{6})\b", RegexOptions.Compiled);

    public string? LastTo { get; private set; }
    public string? LastSubject { get; private set; }
    public string? LastBody { get; private set; }
    public string? LastOtp { get; private set; }
    public int SendCount { get; private set; }

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        LastTo = toEmail;
        LastSubject = subject;
        LastBody = htmlBody;
        LastOtp = OtpRegex.Match(htmlBody ?? string.Empty) is { Success: true } match
            ? match.Groups[1].Value
            : null;
        SendCount++;
        return Task.CompletedTask;
    }
}

public sealed class FakeGoogleIdTokenValidator : IGoogleIdTokenValidator
{
    public Dictionary<string, GoogleIdentity> Tokens { get; } = new(StringComparer.Ordinal);

    public Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (Tokens.TryGetValue(idToken, out var identity))
            return Task.FromResult<GoogleIdentity?>(identity);
        return Task.FromResult<GoogleIdentity?>(null);
    }
}
