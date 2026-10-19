namespace Backend.DTOs.Recommendations;

/// <summary>Request body for V7 Python POST /recommend (Step 2 client).</summary>
public record PythonRecommendationRequest(
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
    string PreferencesText,
    int TopK = 5,
    IReadOnlyList<PythonHistoryItem>? History = null,
    double HistoryWeight = 0,
    // "v7" (original) or "travel" target-segment rules; null = omitted = "v7".
    string? SegmentRules = null);

/// <summary>One past rental for the history boost: catalog car_id when known, else seats only.</summary>
public record PythonHistoryItem(string? CarId, int? Seats, double Weight);

/// <summary>Response body from V7 Python POST /recommend.</summary>
public record PythonRecommendationResponse(
    bool Success,
    string PredictedSegment,
    IReadOnlyList<PythonRecommendationItem> Recommendations,
    int? TrainCount = null);

public record PythonRecommendationItem(
    string CarId,
    string Make,
    string Model,
    string Segment,
    double? Price = null,
    string? FuelType = null,
    int? Seats = null,
    double? SafetyScore = null,
    double? TextSimilarity = null,
    double? StructuredSimilarity = null,
    double? MlSegmentProbability = null,
    double FinalScore = 0,
    double? V7Score = null,
    double? HistoryAffinity = null);
