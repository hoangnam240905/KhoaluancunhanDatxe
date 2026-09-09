using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Bookings;
using Backend.DTOs.Dispatch;
using Backend.DTOs.Incidents;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Backend.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Dispatcher)]
[Route("api/dispatch")]
public class DispatchController(
    DispatchService dispatchService,
    IncidentService incidents,
    VehicleInspectionService inspections) : ControllerBase
{
    [HttpGet("fleet-status")]
    public async Task<ActionResult<DispatchFleetStatusResponse>> FleetStatus()
        => Ok(await dispatchService.GetFleetStatusAsync());

    [HttpGet("bookings/{id:int}/assignable")]
    public async Task<ActionResult<DispatchAssignableResponse>> Assignable(int id)
    {
        var (data, error) = await dispatchService.GetAssignableAsync(id);
        return data is null ? BadRequest(new { message = error ?? "Không thể lấy danh sách khả dụng." }) : Ok(data);
    }

    [HttpPost("bookings/{id:int}/confirm")]
    public async Task<ActionResult<BookingResponse>> Confirm(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (booking, error) = await dispatchService.ConfirmBookingAsync(id, userId);
        return booking is null ? BadRequest(new { message = error ?? "Không thể xác nhận đơn." }) : Ok(booking);
    }

    [HttpPost("bookings/{id:int}/assign")]
    public async Task<ActionResult<BookingResponse>> Assign(int id, AssignTripRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await dispatchService.AssignTripAsync(id, request, userId);
        if (result.Booking is not null)
            return Ok(result.Booking);
        if (result.Conflict is not null)
            return BadRequest(result.Conflict);
        return BadRequest(new { message = result.Error ?? "Không thể phân công chuyến đi." });
    }

    [HttpPost("bookings/{id:int}/handover")]
    public async Task<ActionResult<BookingResponse>> Handover(
        int id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] VehicleConditionRequest? request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (booking, error) = await dispatchService.HandoverSelfDriveAsync(id, userId, request);
        return booking is null ? BadRequest(new { message = error ?? "Không thể giao xe." }) : Ok(booking);
    }

    [HttpPost("bookings/{id:int}/complete")]
    public async Task<ActionResult<BookingResponse>> Complete(
        int id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] VehicleConditionRequest? request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (booking, error) = await dispatchService.CompleteSelfDriveAsync(id, userId, request);
        return booking is null ? BadRequest(new { message = error ?? "Không thể hoàn thành đơn." }) : Ok(booking);
    }

    [HttpGet("incidents")]
    public async Task<ActionResult<List<IncidentResponse>>> GetIncidents(
        [FromQuery] int? bookingId,
        [FromQuery] string? status)
        => Ok(await incidents.GetOperationsAsync(bookingId, status));

    [HttpGet("inspections")]
    public async Task<ActionResult<IReadOnlyList<VehicleInspectionResponse>>> GetInspections(
        [FromQuery] int? bookingId)
        => Ok(await inspections.GetAdminAsync(bookingId));
}
