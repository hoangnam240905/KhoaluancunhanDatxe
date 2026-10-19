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
        var usage = NormalizeUsage(customer.RentalPurpose, options.DefaultUsagePrimary);

        return new CustomerV7Profile(
            Age: ComputeAge(customer.DateOfBirth, asOf, options.DefaultAge),
            Kids: kids,
            FamilySize: familySize,
            IncomeBracket: NormalizeBracket(customer.IncomeRange, "med"),
            CityType: NormalizeCity(customer.CityType, "med"),
            CommuteKmDay: customer.CommuteKmPerDay is >= 0 ? customer.CommuteKmPerDay.Value : options.DefaultCommuteKmDay,
            // Customer schema has no ChargingAccess — fixed fallback (see RecommendationServiceOptions).
            ChargingAccess: options.DefaultChargingAccess is 0 or 1
                ? options.DefaultChargingAccess
                : 0,
            AnnualKm: customer.AnnualMileage is >= 0 ? customer.AnnualMileage.Value : options.DefaultAnnualKm,
            UsagePrimary: usage,
            // A per-day travel budget is checked against real rental prices in Backend (OverBudget);
            // V7 then gets the training median (its price signal is purchase price, not rental).
            BudgetMonthly: customer.TripBudgetPerDay is not null
                ? options.DefaultBudgetMonthly
                : NormalizeBudget(ResolveBudget(customer, options.DefaultBudgetMonthly), options),
            PreferencesText: ResolvePreferences(customer, usage, familySize, options.DefaultPreferencesText));
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

    /// <summary>
    /// Brings a customer budget onto the V7 dataset scale: VND/month (≥ threshold) is divided by
    /// VndPerBudgetUnit, then everything is clamped to the training range so one outlier
    /// (e.g. 49,999,999) cannot dominate the structured vector.
    /// </summary>
    internal static double NormalizeBudget(double budget, RecommendationServiceOptions options)
    {
        var value = budget >= options.VndBudgetThreshold && options.VndPerBudgetUnit > 0
            ? budget / options.VndPerBudgetUnit
            : budget;
        var min = Math.Min(options.MinBudgetMonthly, options.MaxBudgetMonthly);
        var max = Math.Max(options.MinBudgetMonthly, options.MaxBudgetMonthly);
        return Math.Clamp(value, min, max);
    }

    /// <summary>
    /// Text sent to V7's TF-IDF channel: the customer's own note (kept verbatim) followed by tokens
    /// from the car-description vocabulary — mapped from Vietnamese phrases in the note, from the
    /// preference dropdowns and from party size. With no note and no preferences, the trip type
    /// decides. Previously the dropdowns were dropped whenever a note existed, and Vietnamese notes
    /// matched nothing.
    /// </summary>
    internal static string ResolvePreferences(Customer customer, string usage, int partySize, string fallback)
    {
        var note = customer.PreferencesText?.Trim();
        var tokens = new List<string>(PreferenceKeywords.FromNote(note));
        void Add(string? t)
        {
            if (!string.IsNullOrWhiteSpace(t) && !tokens.Contains(t))
                tokens.Add(t);
        }

        Add(PreferenceKeywords.VehicleTypeTokens(customer.PreferredVehicleType));
        Add(PreferenceKeywords.FuelTokens(customer.PreferredFuelType));
        Add(PreferenceKeywords.SeatTokens(customer.PreferredSeats is > 0 ? customer.PreferredSeats : partySize));
        // Transmission is not part of the car descriptions -> it cannot influence V7, so it is not sent.

        if (string.IsNullOrWhiteSpace(note) && tokens.Count <= 1)
            Add(TripTypeTokens(usage));

        var extra = string.Join(" ", tokens);
        if (!string.IsNullOrWhiteSpace(note))
            return extra.Length == 0 ? note : $"{note} | {extra}";
        if (extra.Length > 0)
            return extra;
        return string.IsNullOrWhiteSpace(fallback) ? "general car rental preferences" : fallback.Trim();
    }

    private static string? TripTypeTokens(string usage) => usage switch
    {
        "trips" => "suv",
        "work" => "sedan",
        "city" => "supermini mini",
        "family" => "crossover safety",
        _ => null
    };

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
            // Travel trip types from the Profile form (plan1.md 4.2)
            "family_trip" => "family",
            "mountain_trip" => "trips",
            "business_trip" => "work",
            "city_trip" => "city",
            "commute" or "commuting" => "city",
            "trip" or "travel" or "vacation" => "trips",
            "work" or "business" => "work",
            "family" or "city" or "trips" => v,
            _ => v
        };
    }
}
