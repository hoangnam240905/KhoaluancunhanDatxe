namespace Backend.Validation;

public static class CustomerAccountStatusRules
{
    public const int ReasonMaxLength = 255;

    public const string LockReasonRequired = "Vui lòng nhập lý do khóa tài khoản.";
    public const string LockReasonTooLong = "Lý do khóa không được vượt quá 255 ký tự.";
    public const string InactiveReasonRequired = "Vui lòng nhập lý do vô hiệu hóa.";
    public const string InactiveReasonTooLong = "Lý do vô hiệu hóa không được vượt quá 255 ký tự.";

    public static string? ValidateLockReason(string? reason, out string? normalized)
        => ValidateRequired(reason, LockReasonRequired, LockReasonTooLong, out normalized);

    public static string? ValidateInactiveReason(string? reason, out string? normalized)
        => ValidateRequired(reason, InactiveReasonRequired, InactiveReasonTooLong, out normalized);

    private static string? ValidateRequired(
        string? reason, string required, string tooLong, out string? normalized)
    {
        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            normalized = null;
            return required;
        }

        if (trimmed.Length > ReasonMaxLength)
        {
            normalized = trimmed;
            return tooLong;
        }

        normalized = trimmed;
        return null;
    }
}
