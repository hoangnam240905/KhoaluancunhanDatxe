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
    double FinalScore);
