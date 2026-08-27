using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Bookings;
using Backend.DTOs.Drivers;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Backend.Controllers;

[ApiController]
[Route("api/drivers")]
public class DriversController(DriverService driverService) : ControllerBase
{
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Dispatcher}")]
    [HttpGet]
    public async Task<ActionResult<List<DriverResponse>>> GetAll([FromQuery] string? status)
        => Ok(await driverService.GetDriversAsync(status));

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
}
