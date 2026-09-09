using System.Globalization;
using System.Text.RegularExpressions;

namespace Backend.Validation;

public static class CustomerRegistrationRules
{
    public const string NameRequired = "Vui lòng nhập họ và tên.";
    public const string InvalidGmail = "Vui lòng nhập địa chỉ Gmail hợp lệ.";
    public const string InvalidPhone = "Vui lòng nhập số điện thoại hợp lệ (10 chữ số, bắt đầu bằng 0).";
    public const string InvalidPassword = "Mật khẩu phải có ít nhất 8 ký tự, 1 chữ hoa và 1 ký tự đặc biệt.";
    public const string EmailInUse = "Email đã được sử dụng.";
    public const string PhoneInUse = "Số điện thoại đã được sử dụng.";
    public const string ConfirmPasswordMismatch = "Xác nhận mật khẩu không khớp.";
    public const string EmailNotVerified = "Vui lòng xác minh email trước khi đăng nhập.";

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly Regex GmailRegex = new(@"^[^@\s]+@gmail\.com$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"^0\d{9}$", RegexOptions.Compiled);
    private static readonly Regex PhoneNoiseRegex = new(@"[\s.\-]", RegexOptions.Compiled);

    public static string? ValidateAndNormalize(
        string? fullName,
        string? email,
        string? phone,
        string? password,
        out string normalizedName,
        out string normalizedEmail,
        out string normalizedPhone)
    {
        normalizedName = NormalizeFullName(fullName);
        normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        normalizedPhone = NormalizePhone(phone);

        if (string.IsNullOrEmpty(normalizedName))
            return NameRequired;

        if (!GmailRegex.IsMatch(normalizedEmail))
            return InvalidGmail;

        if (!PhoneRegex.IsMatch(normalizedPhone))
            return InvalidPhone;

        if (!IsStrongPassword(password))
            return InvalidPassword;

        return null;
    }

    public static bool IsValidPhone(string? phone) =>
        !string.IsNullOrEmpty(phone) && PhoneRegex.IsMatch(phone);

    public static string? ValidateCustomerProfile(
        string? fullName,
        string? email,
        string? phone,
        out string normalizedName,
        out string normalizedEmail,
        out string normalizedPhone)
    {
        normalizedName = NormalizeFullName(fullName);
        normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        normalizedPhone = NormalizePhone(phone);

        if (string.IsNullOrEmpty(normalizedName))
            return NameRequired;

        if (!GmailRegex.IsMatch(normalizedEmail))
            return InvalidGmail;

        if (!PhoneRegex.IsMatch(normalizedPhone))
            return InvalidPhone;

        return null;
    }

    public static string? ValidateAdminDriver(
        string? fullName,
        string? email,
        string? phone,
        string? password,
        out string normalizedName,
        out string normalizedEmail,
        out string normalizedPhone)
    {
        normalizedName = NormalizeFullName(fullName);
        normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        normalizedPhone = NormalizePhone(phone);

        if (string.IsNullOrEmpty(normalizedName))
            return NameRequired;

        if (string.IsNullOrEmpty(normalizedEmail) || !normalizedEmail.Contains('@') || normalizedEmail.Contains(' '))
            return "Vui lòng nhập địa chỉ email hợp lệ.";

        if (!PhoneRegex.IsMatch(normalizedPhone))
            return InvalidPhone;

        if (!IsStrongPassword(password))
            return InvalidPassword;

        return null;
    }

    public static string NormalizeFullName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var parts = name.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts.Select(CapitalizeWord));
    }

    public static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;

        return PhoneNoiseRegex.Replace(phone.Trim(), string.Empty);
    }

    public static bool IsStrongPassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8)
            return false;

        if (!Regex.IsMatch(password, "[A-Z]"))
            return false;

        if (!Regex.IsMatch(password, "[^a-zA-Z0-9]"))
            return false;

        return true;
    }

    private static string CapitalizeWord(string word)
    {
        if (word.Length == 1)
            return char.ToUpper(word[0], Vietnamese).ToString();

        return char.ToUpper(word[0], Vietnamese) + word[1..].ToLower(Vietnamese);
    }
}
