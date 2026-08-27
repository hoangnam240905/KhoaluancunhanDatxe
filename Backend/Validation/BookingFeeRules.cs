using Backend.Constants;

namespace Backend.Validation;

public static class BookingFeeRules
{
    public const string InvalidType = "Loại phí không hợp lệ.";
    public const string NegativeAmount = "Số tiền phí không được âm.";
    public const string DescriptionTooLong = "Mô tả phí quá dài.";

    public const int DescriptionMaxLength = 300;

    public static string? Validate(string? feeType, decimal amount, string? description, out string resolvedType)
    {
        if (!BookingFeeTypes.TryResolve(feeType, out resolvedType))
            return InvalidType;

        if (amount < 0)
            return NegativeAmount;

        if (description is { Length: > DescriptionMaxLength })
            return DescriptionTooLong;

        return null;
    }
}
