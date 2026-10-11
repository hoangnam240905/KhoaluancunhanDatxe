using Backend.DTOs.Recommendations;

namespace Backend.Services.Recommendation;

/// <summary>
/// Step 3 orchestrator: Customer (SQLite) → V7 profile → Python /recommend → map car_id → Vehicle (SQLite).
/// No frontend API; no RecommendationLogs writes.
/// </summary>
public interface IRecommendationMappingService
{
    /// <summary>Builds a V7 profile from a Customer entity already loaded from SQLite.</summary>
    CustomerV7Profile BuildV7Profile(Entities.Customer customer, DateOnly? asOf = null);

    /// <summary>
    /// Loads Customer by id, calls V7, maps car_id → Vehicle.
    /// Preserves Python ranking order. May return fewer than topK if some car_ids cannot map.
    /// </summary>
    Task<RecommendationResultDto> GetRecommendationsForCustomerAsync(
        int customerId,
        int topK,
        CancellationToken cancellationToken = default);
}
