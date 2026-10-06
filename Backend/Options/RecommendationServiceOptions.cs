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

    /// <summary>Used when Customer.BudgetMax and BudgetMin are both null.</summary>
    public double DefaultBudgetMonthly { get; set; } = 2000;

    /// <summary>Used when Customer.DateOfBirth is null.</summary>
    public int DefaultAge { get; set; } = 30;

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
