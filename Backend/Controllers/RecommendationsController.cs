using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Recommendations;
using Backend.Services;
using Backend.Services.Recommendation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>
/// Step 4.1: Customer JWT → V7 profile → Python → map car_id → SQLite Vehicle.
/// Step 4.5: on success, write RecommendationLogs (+ optional deduped VIEW). History only — no RF retrain.
/// CustomerId is taken from JWT only (no customerId query — IDOR-safe).
/// </summary>
[ApiController]
[Authorize(Roles = RoleNames.Customer)]
[Route("api/recommendations")]
public class RecommendationsController(
    IRecommendationMappingService mapping,
    RecommendationLogService recommendationLogs,
    VehicleImageService images) : ControllerBase
{
    /// <summary>GET /api/recommendations?topK=5</summary>
    [HttpGet]
    public async Task<ActionResult<CustomerRecommendationApiResponse>> Get(
        [FromQuery] int topK = 5,
        CancellationToken cancellationToken = default)
    {
        // JWT NameIdentifier == Customer.CustomerId for Customer role (same as other customer APIs).
        var customerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var result = await mapping.GetRecommendationsForCustomerAsync(
            customerId, topK, cancellationToken);

        // Only V7-ranked cars are logged as recommendations (large-party rule additions have no V7 score).
        var v7Ranked = result.Recommendations.Where(r => !r.FromBusinessRule).ToList();
        if (result.StatusCode == StatusCodes.Status200OK
            && result.Success
            && v7Ranked.Count > 0)
        {
            // CustomerId from JWT path only; scores are V7 finalScore; does not change ranking.
            await recommendationLogs.LogSuccessfulRecommendationsAsync(
                customerId,
                result.PredictedSegment,
                v7Ranked.Select(r => (r.VehicleId, r.Score)).ToList(),
                createdAt: DateTime.UtcNow,
                alsoLogViewWithDedupe: true,
                cancellationToken: cancellationToken);
        }

        return result.StatusCode switch
        {
            StatusCodes.Status503ServiceUnavailable =>
                StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = result.Reason }),
            StatusCodes.Status404NotFound =>
                NotFound(new { message = result.Reason }),
            StatusCodes.Status400BadRequest =>
                BadRequest(new { message = result.Reason }),
            StatusCodes.Status200OK when result.Success =>
                Ok(ToApiResponse(result, await images.GetPrimaryUrlsAsync(result.Recommendations.Select(r => r.VehicleId)))),
            _ =>
                StatusCode(result.StatusCode is >= 400 and < 600
                        ? result.StatusCode
                        : StatusCodes.Status502BadGateway,
                    new { message = result.Reason ?? "Không lấy được gợi ý." })
        };
    }

    private static CustomerRecommendationApiResponse ToApiResponse(
        RecommendationResultDto result, IReadOnlyDictionary<int, string> primaryImages)
        => new(
            PredictedSegment: result.PredictedSegment ?? string.Empty,
            Recommendations: result.Recommendations
                .Select(r => new CustomerRecommendationApiItem(
                    VehicleId: r.VehicleId,
                    CarId: r.CarId,
                    Brand: r.Brand,
                    Model: r.Model,
                    Segment: r.Segment,
                    FinalScore: r.Score,
                    VehicleStatus: r.VehicleStatus,
                    HistoryAffinity: r.HistoryAffinity,
                    PricePerDay: r.PricePerDay,
                    OverBudget: r.OverBudget,
                    Seats: r.Seats,
                    SeatsOk: r.SeatsOk,
                    MaintenanceDue: r.MaintenanceDue,
                    FromBusinessRule: r.FromBusinessRule,
                    PrimaryImageUrl: primaryImages.GetValueOrDefault(r.VehicleId)))
                .ToList(),
            PartySize: result.PartySize);
}
