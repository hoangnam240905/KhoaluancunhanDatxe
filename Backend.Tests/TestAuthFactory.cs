using Backend.Data;
using Backend.Options;
using Backend.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Backend.Tests;

internal static class TestAuthFactory
{
    public static AuthService Create(CarRentalDbContext db, IConfiguration config)
    {
        var emailOpts = Microsoft.Extensions.Options.Options.Create(new EmailOptions { Enabled = false, ResendCooldownSeconds = 0 });
        var googleOpts = Microsoft.Extensions.Options.Options.Create(new GoogleAuthOptions());
        var otp = new EmailOtpService(db, new CapturingEmailSender(), emailOpts, config, TimeProvider.System);
        return new AuthService(db, new JwtTokenService(config), otp, new FakeGoogleIdTokenValidator(), googleOpts);
    }
}
