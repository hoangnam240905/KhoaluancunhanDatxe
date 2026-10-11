using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Recommendations;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Authorize]
[Route("api/recommendation-logs")]
public class RecommendationLogsController(RecommendationLogService logs) : ControllerBase
{
    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost]
    public async Task<ActionResult<RecommendationLogResponse>> Create(CreateRecommendationLogRequest request)
    {
        var (data, error, status) = await logs.CreateAsync(request);
        return status switch
        {
            StatusCodes.Status201Created when data is not null => StatusCode(StatusCodes.Status201Created, data),
            StatusCodes.Status404NotFound => NotFound(new { message = error }),
            _ => BadRequest(new { message = error })
        };
    }

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Customer}")]
    [HttpGet("customer/{customerId:int}")]
    public async Task<ActionResult<List<RecommendationLogResponse>>> GetByCustomer(int customerId)
    {
        if (User.IsInRole(RoleNames.Customer)
            && int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!) != customerId)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Không có quyền xem nhật ký gợi ý này." });

        var (data, error, status) = await logs.GetByCustomerAsync(customerId);
        return status == StatusCodes.Status404NotFound
            ? NotFound(new { message = error })
            : Ok(data);
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpGet("vehicle/{vehicleId:int}")]
    public async Task<ActionResult<List<RecommendationLogResponse>>> GetByVehicle(int vehicleId)
    {
        var (data, error, status) = await logs.GetByVehicleAsync(vehicleId);
        return status == StatusCodes.Status404NotFound
            ? NotFound(new { message = error })
            : Ok(data);
    }
}
