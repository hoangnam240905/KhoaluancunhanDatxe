namespace Backend.Validation;

public static class BookingDateRules
{
    public const string EndMustBeAfterStart = "Thời gian kết thúc phải sau thời gian bắt đầu.";
    public const string StartCannotBePast = "Ngày bắt đầu không được trong quá khứ.";

    /// Rental days: ceil duration, minimum 1 when EndDate > StartDate.
    public static int QuotedDays(DateTime startDate, DateTime endDate)
        => Math.Max(1, (int)Math.Ceiling((endDate - startDate).TotalDays));

    /// New quote / create booking. Same calendar day with later time is 1 day (existing convention).
    public static string? ValidateNewRental(DateTime startDate, DateTime endDate)
    {
        if (endDate <= startDate)
            return EndMustBeAfterStart;

        if (DateOnly.FromDateTime(startDate) < VietnamTime.Today)
            return StartCannotBePast;

        return null;
    }
}
