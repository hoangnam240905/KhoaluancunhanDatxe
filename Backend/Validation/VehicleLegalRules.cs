namespace Backend.Validation;

public static class VehicleLegalRules
{
    public const int RegistrationNumberMaxLength = 30;
    public const int LicensePlateMaxLength = 20;
    public const int MinYear = 1990;
    public const int MaxYear = 2100;

    public const string TypeNotFound = "Không tìm thấy loại xe.";
    public const string VehicleNotFound = "Không tìm thấy xe.";
    public const string RegistrationTooLong = "Số giấy đăng ký không được vượt quá 30 ký tự.";
    public const string DuplicateRegistration = "Số giấy đăng ký đã được sử dụng.";
    public const string InvalidExpiryDate = "Ngày hết hạn giấy tờ không hợp lệ.";
    public const string LicensePlateRequired = "Vui lòng nhập biển số xe.";
    public const string LicensePlateTooLong = "Biển số xe không được vượt quá 20 ký tự.";
    public const string DuplicateLicensePlate = "Biển số xe đã tồn tại.";
    public const string InvalidYear = "Năm sản xuất phải từ 1990 đến 2100.";

    public static string? NormalizeRegistration(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    /// <summary>
    /// Trim only; keep original casing. Compare uniqueness with <c>ToLower()</c>.
    /// </summary>
    public static string? NormalizeLicensePlate(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    public static string? ValidateLicensePlate(string? value, out string? normalized)
    {
        normalized = NormalizeLicensePlate(value);
        if (normalized is null)
            return LicensePlateRequired;
        if (normalized.Length > LicensePlateMaxLength)
            return LicensePlateTooLong;
        return null;
    }

    public static string? ValidateYear(int year)
        => year is < MinYear or > MaxYear ? InvalidYear : null;

    public static string? ValidateMetadata(
        string? registrationNumber,
        DateOnly? registrationExpiry,
        DateOnly? inspectionExpiry,
        DateOnly? insuranceExpiry,
        out string? normalizedRegistration)
    {
        normalizedRegistration = NormalizeRegistration(registrationNumber);
        if (normalizedRegistration is { Length: > RegistrationNumberMaxLength })
            return RegistrationTooLong;

        if (!IsValidExpiry(registrationExpiry)
            || !IsValidExpiry(inspectionExpiry)
            || !IsValidExpiry(insuranceExpiry))
            return InvalidExpiryDate;

        return null;
    }

    public static bool IsValidExpiry(DateOnly? date)
    {
        if (date is null)
            return true;

        var year = date.Value.Year;
        return year >= MinYear && year <= MaxYear;
    }
}
