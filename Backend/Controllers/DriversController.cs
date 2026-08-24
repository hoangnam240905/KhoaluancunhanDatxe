using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Bookings;
using Backend.DTOs.Drivers;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        var driver = await driverService.UpdateStatusAsync(userId, request.Status);
        return driver is null ? BadRequest() : Ok(driver);
    }

    [Authorize(Roles = RoleNames.Driver)]
    [HttpPost("trips/{assignmentId:int}/accept")]
    public async Task<IActionResult> AcceptTrip(int assignmentId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ok = await driverService.AcceptTripAsync(userId, assignmentId);
        return ok ? Ok(new { message = "Da nhan chuyen." }) : BadRequest();
    }

    [Authorize(Roles = RoleNames.Driver)]
    [HttpPost("trips/{assignmentId:int}/start")]
    public async Task<IActionResult> StartTrip(int assignmentId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ok = await driverService.StartTripAsync(userId, assignmentId);
        return ok ? Ok(new { message = "Da bat dau chuyen." }) : BadRequest();
    }

    [Authorize(Roles = RoleNames.Driver)]
    [HttpPost("trips/{assignmentId:int}/complete")]
    public async Task<IActionResult> CompleteTrip(int assignmentId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ok = await driverService.CompleteTripAsync(userId, assignmentId);
        return ok ? Ok(new { message = "Da hoan thanh chuyen." }) : BadRequest();
    }
}
