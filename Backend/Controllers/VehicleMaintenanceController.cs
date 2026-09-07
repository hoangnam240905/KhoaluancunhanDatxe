using Backend.Constants;
using Backend.DTOs.Maintenance;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/vehicles")]
public class VehicleMaintenanceController(
    VehicleMaintenanceService maintenance,
    MaintenanceAlertService alerts) : ControllerBase
{
    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost("{id:int}/maintenance")]
    public async Task<ActionResult<MaintenanceRecordResponse>> Create(int id, CreateMaintenanceRequest request)
    {
        var (data, error, status) = await maintenance.CreateAsync(id, request);
        return status switch
        {
            201 => CreatedAtAction(nameof(GetHistory), new { id }, data),
            404 => NotFound(new { message = error }),
            _ => BadRequest(new { message = error })
        };
    }

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Dispatcher}")]
    [HttpGet("{id:int}/maintenance-history")]
    public async Task<ActionResult<List<MaintenanceRecordResponse>>> GetHistory(int id)
    {
        var (data, error, status) = await maintenance.GetHistoryAsync(id);
        return status == 404
            ? NotFound(new { message = error })
            : Ok(data);
    }

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Dispatcher}")]
    [HttpGet("maintenance-alerts")]
    public async Task<ActionResult<List<MaintenanceAlertResponse>>> GetAlerts()
        => Ok(await alerts.GetAlertsAsync());
}
