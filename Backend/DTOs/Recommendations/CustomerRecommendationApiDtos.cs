namespace Backend.DTOs.Recommendations;

/// <summary>Step 4.1: public Customer recommendation API response (SQLite vehicles + V7 ranking).</summary>
public record CustomerRecommendationApiResponse(
    string PredictedSegment,
    IReadOnlyList<CustomerRecommendationApiItem> Recommendations);

public record CustomerRecommendationApiItem(
    int VehicleId,
    string CarId,
    string Brand,
    string Model,
    string Segment,
    double FinalScore,
    // Current vehicle status, shown as a hint only: recommendations are not filtered by
    // availability; the booking flow still checks the requested dates.
    string? VehicleStatus = null);
