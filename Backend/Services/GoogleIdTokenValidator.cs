using Backend.Options;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public sealed class GoogleIdTokenValidator(IOptions<GoogleAuthOptions> options) : IGoogleIdTokenValidator
{
    public async Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var clientId = options.Value.ClientId;
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(idToken))
            return null;

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [clientId]
                });

            if (payload is null ||
                string.IsNullOrWhiteSpace(payload.Subject) ||
                string.IsNullOrWhiteSpace(payload.Email))
                return null;

            if (payload.EmailVerified == false)
                return null;

            return new GoogleIdentity(payload.Subject, payload.Email, payload.Name, payload.EmailVerified == true);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
