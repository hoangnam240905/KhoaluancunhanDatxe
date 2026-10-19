using Microsoft.AspNetCore.Mvc;
using PortalWeb.Services;

namespace PortalWeb.Pages.Customer;

/// <summary>
/// Step 4.3: Customer V7 recommendation UI — uses GET /api/recommendations via CarRentalApiClient.
/// Step 4.5: "Đặt xe này" records BOOK interaction (JWT CustomerId) then redirects to booking create.
/// </summary>
public class RecommendationsModel(CarRentalApiClient api, AuthSession auth, ILogger<RecommendationsModel> logger) : RolePageModel
{
    public bool IsLoggedIn => auth.IsLoggedIn;
    public bool IsCustomer => auth.Role == "Customer";
    public string? UserName => auth.FullName;

    public IActionResult OnGet()
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;
        return Page();
    }

    /// <summary>
    /// Step 4.5: record BOOK for the chosen recommended vehicle, then continue to Create booking.
    /// CustomerId comes from Backend JWT; does not invent Favorite.
    /// </summary>
    public async Task<IActionResult> OnGetBookAsync(int vehicleId)
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;

        if (vehicleId <= 0)
            return RedirectToPage("/Customer/Bookings/Create", new { fromRecommendation = true });

        // Intent only: the real BOOK is recorded by Backend when a booking with this vehicle is created.
        var (data, error, status) = await api.CreateCustomerVehicleInteractionAsync(vehicleId, "CLICK_BOOK");
        if (data is null)
            logger.LogWarning(
                "BOOK interaction failed for VehicleId {VehicleId}: {Status} {Error}",
                vehicleId, status, error);
        // Still navigate to booking — interaction is history only, must not block rent flow.

        return RedirectToPage("/Customer/Bookings/Create", new
        {
            vehicleId,
            fromRecommendation = true
        });
    }

    /// <summary>JSON for client loading/empty/error states. Does not send customerId.</summary>
    public async Task<IActionResult> OnGetFetchAsync()
    {
        if (!auth.IsLoggedIn)
            return new JsonResult(new { message = "Phiên đăng nhập hết hạn." }) { StatusCode = StatusCodes.Status401Unauthorized };
        if (auth.Role != "Customer")
            return new JsonResult(new { message = "Không có quyền truy cập." }) { StatusCode = StatusCodes.Status403Forbidden };

        try
        {
            var (data, error, status) = await api.GetRecommendationsAsync(topK: 5);

            if (status is 401 or 403)
                return new JsonResult(new { message = error ?? "Phiên đăng nhập hết hạn." }) { StatusCode = status };

            if (status == 503)
            {
                logger.LogWarning("Recommendation API 503: {Error}", error);
                return StatusCode(503, new { message = error ?? "Dịch vụ gợi ý xe tạm thời không khả dụng." });
            }

            if (data is null)
            {
                logger.LogWarning("Recommendation API failed ({Status}): {Error}", status, error);
                return StatusCode(status is >= 400 and < 600 ? status : 502,
                    new { message = error ?? "Không lấy được gợi ý xe." });
            }

            var items = data.Recommendations
                .GroupBy(r => r.VehicleId)
                .Select(g => g.First())
                .Take(5)
                .ToList();

            if (items.Count != data.Recommendations.Count)
                logger.LogWarning("Recommendation response had duplicate VehicleId; deduped for UI.");

            return new JsonResult(new
            {
                predictedSegment = data.PredictedSegment,
                recommendations = items.Select(r => new
                {
                    vehicleId = r.VehicleId,
                    carId = r.CarId,
                    brand = r.Brand,
                    model = r.Model,
                    segment = r.Segment,
                    finalScore = r.FinalScore,
                    vehicleStatus = r.VehicleStatus,
                    historyAffinity = r.HistoryAffinity
                })
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Recommendation fetch failed unexpectedly.");
            return StatusCode(503, new { message = "Không thể tải gợi ý xe. Vui lòng thử lại." });
        }
    }
}
