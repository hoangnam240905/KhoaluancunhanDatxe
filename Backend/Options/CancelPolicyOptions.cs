using Backend.Constants;

namespace Backend.Options;

/// <summary>Current cancel policy. New bookings/contracts snapshot it; existing ones keep their own value.</summary>
public class CancelPolicyOptions
{
    public const string SectionName = "CancelPolicy";

    public int MinHoursBeforeStart { get; set; } = CancelPolicy.DefaultMinHoursBeforeStart;
}
