namespace Backend.DTOs.Recommendations;

/// <summary>
/// Step 3: Backend recommendation result after Customer → V7 → Vehicle mapping.
/// Vehicle fields come from SQLite; ranking/score come from Python.
/// </summary>
public record RecommendationResultDto(
    bool Success,
    int CustomerId,
    string? PredictedSegment,
    IReadOnlyList<RecommendationItemDto> Recommendations,
    string? Reason = null,
    int StatusCode = 200,
    int UnmappedCount = 0,
    int AmbiguousCount = 0,
    int DuplicateVehicleCount = 0,
    int? PartySize = null,
    // Python candidates received and how many mapped to fleet vehicles before the Top-K cut
    // (they differ from Top-K when the party size enlarges the candidate pool).
    int CandidateCount = 0,
    int MappedCount = 0);

public record RecommendationItemDto(
    int VehicleId,
    string CarId,
    string Brand,
    string Model,
    string Segment,
    double Score,
    string? LicensePlate = null,
    string? VehicleType = null,
    string? FuelType = null,
    string? Transmission = null,
    string? VehicleStatus = null,
    double? HistoryAffinity = null,
    decimal? PricePerDay = null,
    bool? OverBudget = null,
    int? Seats = null,
    bool? SeatsOk = null,
    bool? MaintenanceDue = null,
    bool FromBusinessRule = false);

/// <summary>
/// Intermediate V7 profile built from a real Customer entity (no User PII).
/// </summary>
public record CustomerV7Profile(
    int Age,
    int Kids,
    int FamilySize,
    string IncomeBracket,
    string CityType,
    double CommuteKmDay,
    int ChargingAccess,
    double AnnualKm,
    string UsagePrimary,
    double BudgetMonthly,
    string PreferencesText);
