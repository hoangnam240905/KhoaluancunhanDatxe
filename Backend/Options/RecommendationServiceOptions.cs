namespace Backend.Options;

/// <summary>HTTP client options for the standalone V7 Python RecommendationService (port 8000).</summary>
public class RecommendationServiceOptions
{
    public const string SectionName = "RecommendationService";

    public string BaseUrl { get; set; } = "http://127.0.0.1:8000";
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// V7 requires charging_access, but Customer master schema has no ChargingAccess column.
    /// Fixed fallback only — do not invent per-customer values. Schema change is out of Step 3 scope.
    /// </summary>
    public int DefaultChargingAccess { get; set; } = 0;

    /// <summary>
    /// Used when the customer has no monthly budget, or has a per-day travel budget (checked separately).
    /// Median budget_monthly of the V7 training users.
    /// </summary>
    public double DefaultBudgetMonthly { get; set; } = 2276;

    /// <summary>
    /// Customer budgets at or above this value are treated as VND/month (Profile asks VND/tháng);
    /// smaller values are assumed to already be in the V7 dataset unit (older rows).
    /// </summary>
    public double VndBudgetThreshold { get; set; } = 100_000;

    /// <summary>
    /// ASSUMPTION (no real exchange rate exists for the synthetic dataset): 5,600 VND per dataset
    /// budget unit maps a 5–30 million VND/month rental budget onto the dataset range 900–5,315.
    /// </summary>
    public double VndPerBudgetUnit { get; set; } = 5_600;

    /// <summary>Range of budget_monthly in the V7 training users; values are clamped into it.</summary>
    public double MinBudgetMonthly { get; set; } = 900;

    public double MaxBudgetMonthly { get; set; } = 5_315;

    /// <summary>
    /// Weight of the rental-history boost blended after the V7 score (0 = pure V7).
    /// Extension only — not evaluated offline (the synthetic dataset has no rental history).
    /// </summary>
    public double HistoryWeight { get; set; } = 0.2;

    /// <summary>A past rental counts half after this many days (recency decay).</summary>
    public double HistoryHalfLifeDays { get; set; } = 90;

    /// <summary>Weight of a booking the customer actually received (RENT) vs only booked (BOOK).</summary>
    public double RentEventWeight { get; set; } = 1.0;

    public double BookEventWeight { get; set; } = 0.5;

    /// <summary>Only the most recent N bookings are sent to Python.</summary>
    public int MaxHistoryBookings { get; set; } = 20;

    /// <summary>Used when Customer.DateOfBirth is null. Median age of the V7 training users.</summary>
    public int DefaultAge { get; set; } = 34;

    /// <summary>
    /// Used when the Profile has no value. Medians of the V7 training users — 0 would sit outside
    /// the training range (annual_km is 3,000–35,000) and skew the Random Forest.
    /// Rental-for-travel customers are not asked these commuter questions.
    /// </summary>
    public double DefaultCommuteKmDay { get; set; } = 22;

    public double DefaultAnnualKm { get; set; } = 14_237.5;

    /// <summary>usage_primary when the trip type is empty (travel rental: family trip).</summary>
    public string DefaultUsagePrimary { get; set; } = "family";

    /// <summary>Used when Customer.FamilySize is null (after kids known).</summary>
    public int DefaultFamilySize { get; set; } = 2;

    /// <summary>Used when PreferencesText (and preferred-* composition) is empty — Python requires non-empty text.</summary>
    public string DefaultPreferencesText { get; set; } = "general car rental preferences";

    /// <summary>
    /// Optional explicit overrides: Python car_id → SQLite VehicleId.
    /// Empty by default. Prefer Brand+Model matching; use overrides only after data preparation.
    /// </summary>
    public Dictionary<string, int> CarIdToVehicleId { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
