using System.Text.RegularExpressions;
using Backend.DTOs.Auth;
using Backend.Options;
using Backend.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// Registering on a machine without the Gmail secrets used to answer "Đã gửi mã OTP" while nothing was sent.
/// Now: Development prints the code to the Backend log and says so; elsewhere it is a clear error.
/// </summary>
public class OtpDeliveryStatusTests
{
    private static RegisterCustomerRequest NewCustomer(string email) =>
        new(email, "Password@123", "Bạn Của Tôi", "0938000111", null, null, null, "Password@123");

    [Fact]
    public async Task Without_smtp_outside_development_registration_fails_clearly_and_creates_no_account()
    {
        using var iso = new IsolatedCarRentalDb();
        var auth = Auth(iso, new NullEmailSender(NullLogger<NullEmailSender>.Instance), "Production", new ListLogger());

        var (data, error) = await auth.RegisterCustomerAsync(NewCustomer("ban.cua.toi@gmail.com"));

        Assert.Null(data);
        Assert.Equal(EmailOtpService.NotConfigured, error);
        Assert.DoesNotContain(iso.Db.Users, u => u.Email == "ban.cua.toi@gmail.com");
        Assert.Equal(EmailOtpService.NotConfigured,
            (await auth.ForgotPasswordAsync(new ForgotPasswordRequest("customer1@gmail.com"))).Message);
    }

    [Fact]
    public async Task Without_smtp_in_development_the_code_is_logged_says_so_and_verifies()
    {
        using var iso = new IsolatedCarRentalDb();
        var log = new ListLogger();
        var auth = Auth(iso, new NullEmailSender(NullLogger<NullEmailSender>.Instance), "Development", log);

        var (data, error) = await auth.RegisterCustomerAsync(NewCustomer("ban.cua.toi@gmail.com"));

        Assert.Null(error);
        Assert.Equal(EmailOtpService.DevConsoleNotice, data!.Message);
        var line = Assert.Single(log.Lines, l => l.Contains("ban.cua.toi@gmail.com"));
        var code = Regex.Match(line, @"\b\d{6}\b").Value;
        var (verified, verifyError) = await auth.VerifyEmailAsync(new VerifyEmailRequest("ban.cua.toi@gmail.com", code));
        Assert.Null(verifyError);
        Assert.NotNull(verified);
    }

    [Fact]
    public async Task Smtp_failure_is_reported_instead_of_crashing()
    {
        using var iso = new IsolatedCarRentalDb();
        var auth = Auth(iso, new ThrowingEmailSender(), "Production", new ListLogger());

        var (data, error) = await auth.RegisterCustomerAsync(NewCustomer("ban.cua.toi@gmail.com"));

        Assert.Null(error);
        Assert.Equal(EmailOtpService.SendFailed, data!.Message); // account kept; "Gửi lại mã" can retry
    }

    [Fact]
    public async Task With_smtp_the_message_is_unchanged()
    {
        using var iso = new IsolatedCarRentalDb();
        var sender = new CapturingEmailSender();
        var auth = Auth(iso, sender, "Production", new ListLogger());

        var (data, _) = await auth.RegisterCustomerAsync(NewCustomer("ban.cua.toi@gmail.com"));

        Assert.Equal(EmailOtpService.SentVerification, data!.Message);
        Assert.Equal(1, sender.SendCount);
    }

    private static AuthService Auth(IsolatedCarRentalDb iso, IEmailSender sender, string environment, ListLogger log)
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationManager();
        config["Jwt:Key"] = "CarRentalSystem_SuperSecretKey_2026_DoAnCuNhan!";
        config["Jwt:Issuer"] = "CarRentalAPI";
        config["Jwt:Audience"] = "CarRentalClients";
        config["Jwt:ExpireHours"] = "8";
        var emailOpts = Microsoft.Extensions.Options.Options.Create(new EmailOptions { ResendCooldownSeconds = 0 });
        var otp = new EmailOtpService(iso.Db, sender, emailOpts, config, TimeProvider.System, new Env(environment), log);
        return new AuthService(iso.Db, new JwtTokenService(config), otp, new FakeGoogleIdTokenValidator(),
            Microsoft.Extensions.Options.Options.Create(new GoogleAuthOptions()));
    }

    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Backend";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class ThrowingEmailSender : IEmailSender
    {
        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
            => throw new System.Net.Mail.SmtpException("535 Username and Password not accepted");
    }

    private sealed class ListLogger : ILogger<EmailOtpService>
    {
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Lines.Add(formatter(state, exception));
    }
}
