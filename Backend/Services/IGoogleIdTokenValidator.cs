namespace Backend.Services;

public sealed record GoogleIdentity(string Subject, string Email, string? Name, bool EmailVerified);

public interface IGoogleIdTokenValidator
{
    Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
