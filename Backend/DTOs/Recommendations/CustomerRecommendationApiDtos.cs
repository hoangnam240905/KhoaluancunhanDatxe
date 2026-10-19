namespace Backend.DTOs.Recommendations;

/// <summary>Step 4.1: public Customer recommendation API response (SQLite vehicles + V7 ranking).</summary>
public record CustomerRecommendationApiResponse(
    string PredictedSegment,
    IReadOnlyList<CustomerRecommendationApiItem> Recommendations,
    // "Số người đi" from the profile (null when unknown): drives seats-first ordering.
    int? PartySize = null);

public record CustomerRecommendationApiItem(
    int VehicleId,
    string CarId,
    string Brand,
    string Model,
    string Segment,
    double FinalScore,
    // Current vehicle status, shown as a hint only: recommendations are not filtered by
    // availability; the booking flow still checks the requested dates.
    string? VehicleStatus = null,
    // Share of the customer's past rentals matching this car (0 when no history).
    double? HistoryAffinity = null,
    // Real rental price of the vehicle type and whether it exceeds the customer's per-day budget
    // (null when the customer set no budget). Hint only — nothing is filtered.
    decimal? PricePerDay = null,
    bool? OverBudget = null,
    // Seats of the car, whether they fit the party, whether bookings are blocked by maintenance,
    // and whether the car was added by the large-party rule (not ranked by V7).
    int? Seats = null,
    bool? SeatsOk = null,
    bool? MaintenanceDue = null,
    bool FromBusinessRule = false);
