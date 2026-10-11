using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Vehicles;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/vehicles")]
public class VehicleStatusHistoryController(VehicleStatusHistoryService history) : ControllerBase
{
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Dispatcher}")]
    [HttpGet("{vehicleId:int}/status-history")]
    public async Task<ActionResult<List<VehicleStatusHistoryResponse>>> GetByVehicle(int vehicleId)
    {
        var (data, error, status) = await history.GetByVehicleAsync(vehicleId);
        return status == StatusCodes.Status404NotFound
            ? NotFound(new { message = error })
            : Ok(data);
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost("{vehicleId:int}/status-history")]
    public async Task<ActionResult<VehicleStatusHistoryResponse>> Create(
        int vehicleId, CreateVehicleStatusHistoryRequest request)
    {
        int? actor = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        var (data, error, status) = await history.CreateAsync(vehicleId, request, actor);
        return status switch
        {
            StatusCodes.Status201Created when data is not null => StatusCode(StatusCodes.Status201Created, data),
            StatusCodes.Status404NotFound => NotFound(new { message = error }),
            _ => BadRequest(new { message = error })
        };
    }
}
