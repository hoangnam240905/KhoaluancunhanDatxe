namespace Backend.Validation;

/// Vietnam wall-clock for rental-date comparisons. Does not change stored UTC timestamps.
public static class VietnamTime
{
    public static TimeZoneInfo Zone { get; } = Resolve();

    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

    public static DateOnly Today => DateOnly.FromDateTime(Now);

    private static TimeZoneInfo Resolve()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
    }
}
