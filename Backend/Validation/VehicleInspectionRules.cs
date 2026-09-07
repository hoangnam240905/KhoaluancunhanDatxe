using Backend.Constants;

namespace Backend.Validation;

public static class VehicleInspectionRules
{
    public const string InvalidType = "Loại kiểm tra xe không hợp lệ.";
    public const string NegativeOdometer = "Số km không được âm.";
    public const string InvalidFuel = "Mức nhiên liệu phải từ 0 đến 100.";
    public const string ConditionTooLong = "Tình trạng xe quá dài.";
    public const string NotesTooLong = "Ghi chú quá dài.";
    public const string OdometerBelowCurrent = "Số km trả xe không được nhỏ hơn số km hiện tại của xe.";
    public const string OdometerBelowHandover = "Số km trả xe không được nhỏ hơn số km lúc giao xe.";

    public const int ConditionMaxLength = 100;
    public const int NotesMaxLength = 500;

    public static string? Validate(
        string? inspectionType,
        decimal? odometerKm,
        decimal? fuelLevel,
        string? condition,
        string? notes,
        out string resolvedType,
        string? exteriorCondition = null,
        string? technicalCondition = null)
    {
        if (!VehicleInspectionTypes.TryResolve(inspectionType, out resolvedType))
            return InvalidType;

        if (odometerKm is < 0)
            return NegativeOdometer;

        if (fuelLevel is < 0 or > 100)
            return InvalidFuel;

        if (condition is { Length: > ConditionMaxLength })
            return ConditionTooLong;

        if (notes is { Length: > NotesMaxLength })
            return NotesTooLong;

        if (exteriorCondition is { Length: > ConditionMaxLength } ||
            technicalCondition is { Length: > ConditionMaxLength })
            return ConditionTooLong;

        return null;
    }

    public static string? ValidateReturnOdometer(decimal? odometerKm, int currentKm)
    {
        if (odometerKm is not null && odometerKm < currentKm)
            return OdometerBelowCurrent;
        return null;
    }

    public static string? ValidateReturnVsHandover(decimal? returnOdometer, decimal? handoverOdometer)
    {
        if (returnOdometer is not null && handoverOdometer is not null && returnOdometer < handoverOdometer)
            return OdometerBelowHandover;
        return null;
    }

    public static decimal? ResolveActualKm(decimal? handoverOdometer, decimal? returnOdometer)
    {
        if (handoverOdometer is null || returnOdometer is null)
            return null;
        return Math.Max(0, returnOdometer.Value - handoverOdometer.Value);
    }
}
