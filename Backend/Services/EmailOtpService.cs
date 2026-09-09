using System.Security.Cryptography;
using System.Text;
using Backend.Constants;
using Backend.Data;
using Backend.Entities;
using Backend.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public class EmailOtpService(
    CarRentalDbContext db,
    IEmailSender emailSender,
    IOptions<EmailOptions> emailOptions,
    IConfiguration configuration,
    TimeProvider timeProvider)
{
    public const string InvalidOtp = "Mã OTP không đúng hoặc đã hết hạn.";
    public const string ExpiredOtp = "Mã OTP đã hết hạn.";
    public const string UsedOtp = "Mã OTP đã được sử dụng.";
    public const string MaxAttempts = "Bạn đã nhập sai quá số lần cho phép. Vui lòng gửi lại mã OTP.";
    public const string Cooldown = "Vui lòng đợi trước khi gửi lại mã OTP.";
    public const string SentVerification = "Đã gửi mã OTP đến email của bạn.";
    public const string ForgotGeneric = "Nếu email tồn tại, mã OTP đã được gửi.";

    public async Task<(bool Ok, string? Error)> IssueAsync(string email, string purpose, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeEmail(email);
        if (string.IsNullOrEmpty(normalized))
            return (false, InvalidOtp);

        var settings = emailOptions.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var cooldown = TimeSpan.FromSeconds(settings.ResendCooldownSeconds <= 0 ? 0 : settings.ResendCooldownSeconds);

        var last = await db.EmailOtps
            .Where(x => x.Email == normalized && x.Purpose == purpose)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (last is not null && cooldown > TimeSpan.Zero && last.CreatedAt > now - cooldown)
            return (false, Cooldown);

        var unused = await db.EmailOtps
            .Where(x => x.Email == normalized && x.Purpose == purpose && x.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var row in unused)
            row.UsedAt = now;

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var expiryMinutes = settings.OtpExpiryMinutes <= 0 ? 5 : settings.OtpExpiryMinutes;
        db.EmailOtps.Add(new EmailOtp
        {
            Email = normalized,
            Purpose = purpose,
            CodeHash = HashOtp(normalized, purpose, code),
            ExpiresAt = now.AddMinutes(expiryMinutes),
            AttemptCount = 0,
            CreatedAt = now
        });
        await db.SaveChangesAsync(cancellationToken);

        var isReset = purpose == OtpPurposes.PasswordReset;
        var subject = isReset
            ? "Mã đặt lại mật khẩu Car Rental"
            : "Mã xác minh email Car Rental";
        var purposeLabel = isReset ? "đặt lại mật khẩu" : "xác minh email";
        var html = $"""
            <p>Đây là email từ hệ thống <strong>Car Rental</strong>.</p>
            <p>Mục đích: {purposeLabel}.</p>
            <p>Mã OTP của bạn là <strong>{code}</strong>.</p>
            <p>Mã hết hạn sau {expiryMinutes} phút. Không chia sẻ mã này cho bất kỳ ai.</p>
            """;
        await emailSender.SendAsync(normalized, subject, html, cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> VerifyAsync(string email, string purpose, string? otp, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeEmail(email);
        var code = (otp ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(normalized) || code.Length != 6 || !code.All(char.IsDigit))
            return (false, InvalidOtp);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var maxAttempts = emailOptions.Value.MaxAttempts <= 0 ? 5 : emailOptions.Value.MaxAttempts;

        var row = await db.EmailOtps
            .Where(x => x.Email == normalized && x.Purpose == purpose)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return (false, InvalidOtp);

        if (row.AttemptCount >= maxAttempts)
            return (false, MaxAttempts);

        if (row.UsedAt is not null)
            return (false, UsedOtp);

        if (row.ExpiresAt <= now)
            return (false, ExpiredOtp);

        var expected = Convert.FromHexString(row.CodeHash);
        var actual = Convert.FromHexString(HashOtp(normalized, purpose, code));
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            row.AttemptCount += 1;
            if (row.AttemptCount >= maxAttempts)
                row.UsedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            return (false, row.AttemptCount >= maxAttempts ? MaxAttempts : InvalidOtp);
        }

        row.UsedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public static string NormalizeEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

    private string HashOtp(string email, string purpose, string code)
    {
        var pepper = emailOptions.Value.OtpPepper;
        if (string.IsNullOrWhiteSpace(pepper))
            pepper = configuration["Jwt:Key"] ?? "CarRentalOtpPepper";

        var payload = Encoding.UTF8.GetBytes($"{email}|{purpose}|{code}");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(pepper));
        return Convert.ToHexString(hmac.ComputeHash(payload));
    }
}
