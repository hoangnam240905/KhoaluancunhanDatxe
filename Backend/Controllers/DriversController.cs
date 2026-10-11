using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Bookings;
using Backend.DTOs.Drivers;
using Backend.DTOs.Incidents;
using Backend.Services;
using Backend.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Backend.Controllers;

[ApiController]
[Route("api/drivers")]
public class DriversController(
    DriverService driverService,
    IncidentService incidentService,
    DriverEligibilityService driverEligibility) : ControllerBase
{
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Dispatcher}")]
    [HttpGet]
    public async Task<ActionResult<List<DriverResponse>>> GetAll([FromQuery] string? status)
        => Ok(await driverService.GetDriversAsync(status));

    [Authorize(Roles = $"{RoleNames.Customer},{RoleNames.Dispatcher},{RoleNames.Admin}")]
    [HttpGet("available")]
    public async Task<ActionResult<IReadOnlyList<CustomerDriverOptionResponse>>> GetAvailable(
        [FromQuery] int? vehicleId,
        [FromQuery] int? vehicleTypeId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        if (BookingDateRules.ValidateNewRental(startDate, endDate) is string dateError)
            return BadRequest(new { message = dateError });
        if (vehicleId is null && vehicleTypeId is null)
            return BadRequest(new { message = "Vui lòng chọn xe hoặc loại xe." });

        return Ok(await driverEligibility.ListSelectableAsync(vehicleId, vehicleTypeId, startDate, endDate));
    }

    [Authorize(Roles = $"{RoleNames.Customer},{RoleNames.Dispatcher},{RoleNames.Admin}")]
    [HttpGet("{id:int}/schedule")]
    public async Task<ActionResult<DriverScheduleResponse>> GetSchedule(
        int id,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        if (endDate <= startDate)
            return BadRequest(new { message = "Thời gian kết thúc phải sau thời gian bắt đầu." });

        var schedule = await driverEligibility.GetScheduleAsync(id, startDate, endDate);
        return schedule is null ? NotFound() : Ok(schedule);
    }

    [Authorize(Roles = RoleNames.Driver)]
    [HttpGet("me")]
    public async Task<ActionResult<DriverResponse>> GetMe()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var driver = await driverService.GetDriverAsync(userId);
        return driver is null ? NotFound() : Ok(driver);
    }

    [Authorize(Roles = RoleNames.Driver)]
    [HttpGet("me/trips")]
    public async Task<ActionResult<List<BookingResponse>>> GetMyTrips()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await driverService.GetDriverTripsAsync(userId));
    }

    [Authorize(Roles = RoleNames.Driver)]
    [HttpPatch("me/status")]
    public async Task<ActionResult<DriverResponse>> UpdateMyStatus(UpdateDriverStatusRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (driver, error) = await driverService.UpdateStatusAsync(userId, request.Status);
        return driver is null ? BadRequest(new { message = error ?? "Không thể cập nhật trạng thái." }) : Ok(driver);
    }

    [Authorize(Roles = RoleNames.Driver)]
    [HttpPost("trips/{assignmentId:int}/accept")]
    public async Task<IActionResult> AcceptTrip(int assignmentId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ok = await driverService.AcceptTripAsync(userId, assignmentId);
        return ok ? Ok(new { message = "Đã nhận chuyến." }) : BadRequest();
    }

    [Authorize(Roles = RoleNames.Driver)]
    [HttpPost("trips/{assignmentId:int}/start")]
    public async Task<IActionResult> StartTrip(int assignmentId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ok = await driverService.StartTripAsync(userId, assignmentId);
        return ok ? Ok(new { message = "Đã bắt đầu chuyến." }) : BadRequest();
    }

    [Authorize(Roles = RoleNames.Driver)]
    [HttpPost("trips/{assignmentId:int}/complete")]
    public async Task<IActionResult> CompleteTrip(
        int assignmentId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] VehicleConditionRequest? request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (ok, error) = await driverService.CompleteTripAsync(userId, assignmentId, request);
        if (ok)
            return Ok(new { message = "Đã hoàn thành chuyến." });
        return error is null ? BadRequest() : BadRequest(new { message = error });
    }

    [Authorize(Roles = RoleNames.Driver)]
    [HttpGet("me/incidents")]
    public async Task<ActionResult<List<IncidentResponse>>> GetMyIncidents()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (incidents, _, _) = await incidentService.GetByDriverAsync(userId);
        return Ok(incidents);
    }

    [Authorize(Roles = RoleNames.Driver)]
    [HttpPost("trips/{assignmentId:int}/incidents")]
    public async Task<ActionResult<IncidentResponse>> ReportIncident(int assignmentId, CreateIncidentRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (incident, error, status) = await incidentService.CreateAsync(userId, assignmentId, request);
        if (incident is not null)
            return Created($"/api/drivers/me/incidents", incident);

        return status switch
        {
            StatusCodes.Status403Forbidden => Forbid(),
            StatusCodes.Status404NotFound => NotFound(new { message = error }),
            _ => BadRequest(new { message = error ?? "Không thể báo sự cố." })
        };
    }
}
