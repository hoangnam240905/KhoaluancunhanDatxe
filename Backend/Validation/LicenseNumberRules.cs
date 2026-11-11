namespace Backend.Validation;

/// <summary>
/// Driving-licence number (giấy phép lái xe). The current card licence (PET, issued since 2013 and still the
/// form under Luật 36/2024) carries a 12-digit number. Applied to numbers that are entered or changed; numbers
/// already stored in an older format are left as they are until someone edits them.
/// </summary>
public static class LicenseNumberRules
{
    public const int Length = 12;
    public const string Invalid = "Số bằng lái gồm đúng 12 chữ số (in ở mục \"Số / No.\" trên bằng lái).";
    public const string Taken = "Số bằng lái này đã được dùng cho một tài khoản khác.";

    /// <summary>Spaces removed (people often type "7901 2345 6789"); null when nothing was typed.</summary>
    public static string? Normalize(string? raw)
    {
        if (raw is null) return null;
        var compact = new string(raw.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        return compact.Length == 0 ? null : compact;
    }

    public static bool IsValid(string? number)
        => number is { Length: Length } && number.All(char.IsAsciiDigit);
}
