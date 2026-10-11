using Backend.DTOs.Recommendations;
using Backend.Entities;
using Backend.Options;

namespace Backend.Services.Recommendation;

/// <summary>
/// Builds a V7 recommendation profile from a real Customer entity (read-only).
/// Does not include User PII (Email / FullName / Phone).
/// </summary>
public static class CustomerV7ProfileBuilder
{
    public static CustomerV7Profile Build(
        Customer customer,
        RecommendationServiceOptions options,
        DateOnly? today = null)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(options);

        var asOf = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var kids = customer.NumberOfChildren ?? 0;
        if (kids < 0) kids = 0;

        var familySize = customer.FamilySize
            ?? Math.Max(options.DefaultFamilySize, kids + 1);
        if (familySize < 1)
            familySize = Math.Max(1, kids + 1);

        return new CustomerV7Profile(
            Age: ComputeAge(customer.DateOfBirth, asOf, options.DefaultAge),
            Kids: kids,
            FamilySize: familySize,
            IncomeBracket: NormalizeBracket(customer.IncomeRange, "med"),
            CityType: NormalizeCity(customer.CityType, "med"),
            CommuteKmDay: customer.CommuteKmPerDay is >= 0 ? customer.CommuteKmPerDay.Value : 0,
            // Customer schema has no ChargingAccess — fixed fallback (see RecommendationServiceOptions).
            ChargingAccess: options.DefaultChargingAccess is 0 or 1
                ? options.DefaultChargingAccess
                : 0,
            AnnualKm: customer.AnnualMileage is >= 0 ? customer.AnnualMileage.Value : 0,
            UsagePrimary: NormalizeUsage(customer.RentalPurpose, "city"),
            BudgetMonthly: ResolveBudget(customer, options.DefaultBudgetMonthly),
            PreferencesText: ResolvePreferences(customer, options.DefaultPreferencesText));
    }

    public static PythonRecommendationRequest ToPythonRequest(CustomerV7Profile profile, int topK)
        => new(
            Age: profile.Age,
            Kids: profile.Kids,
            FamilySize: profile.FamilySize,
            IncomeBracket: profile.IncomeBracket,
            CityType: profile.CityType,
            CommuteKmDay: profile.CommuteKmDay,
            ChargingAccess: profile.ChargingAccess,
            AnnualKm: profile.AnnualKm,
            UsagePrimary: profile.UsagePrimary,
            BudgetMonthly: profile.BudgetMonthly,
            PreferencesText: profile.PreferencesText,
            TopK: topK);

    internal static int ComputeAge(DateOnly? dateOfBirth, DateOnly asOf, int fallbackAge)
    {
        if (dateOfBirth is null)
            return ClampAge(fallbackAge);

        var age = asOf.Year - dateOfBirth.Value.Year;
        if (dateOfBirth.Value > asOf.AddYears(-age))
            age--;

        if (age is < 16 or > 100)
            return ClampAge(fallbackAge);

        return age;
    }

    /// <summary>BudgetMax → BudgetMin → fixed fallback. Never random.</summary>
    internal static double ResolveBudget(Customer customer, double fallback)
    {
        if (customer.BudgetMax is double max && max >= 0)
            return max;
        if (customer.BudgetMin is double min && min >= 0)
            return min;
        return fallback >= 0 ? fallback : 2000;
    }

    internal static string ResolvePreferences(Customer customer, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(customer.PreferencesText))
            return customer.PreferencesText.Trim();

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(customer.PreferredVehicleType))
            parts.Add($"preferred vehicle type {customer.PreferredVehicleType.Trim()}");
        if (!string.IsNullOrWhiteSpace(customer.PreferredFuelType))
            parts.Add($"preferred fuel {customer.PreferredFuelType.Trim()}");
        if (!string.IsNullOrWhiteSpace(customer.PreferredTransmission))
            parts.Add($"preferred transmission {customer.PreferredTransmission.Trim()}");
        if (customer.PreferredSeats is int seats and > 0)
            parts.Add($"preferred seats {seats}");

        if (parts.Count > 0)
            return string.Join(", ", parts);

        return string.IsNullOrWhiteSpace(fallback) ? "general car rental preferences" : fallback.Trim();
    }

    private static int ClampAge(int age) => Math.Clamp(age, 16, 100);

    private static string NormalizeBracket(string? value, string fallback)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(v)) return fallback;
        return v switch
        {
            "medium" or "mid" or "middle" => "med",
            "high" or "low" or "med" => v,
            _ => v
        };
    }

    private static string NormalizeCity(string? value, string fallback)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(v)) return fallback;
        return v switch
        {
            "urban" or "big" or "metro" => "large",
            "suburban" or "medium" or "mid" => "med",
            "rural" or "countryside" => "small",
            "large" or "med" or "small" => v,
            _ => v
        };
    }

    private static string NormalizeUsage(string? value, string fallback)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(v)) return fallback;
        return v switch
        {
            "commute" or "commuting" => "city",
            "trip" or "travel" or "vacation" => "trips",
            "work" or "business" => "work",
            "family" or "city" or "trips" => v,
            _ => v
        };
    }
}
