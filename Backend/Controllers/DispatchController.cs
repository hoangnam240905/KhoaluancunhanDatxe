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
        var result = await dispatchService.ConfirmBookingAsync(id, userId);
        if (result.Booking is not null)
            return Ok(result.Booking);
        if (result.Conflict is not null)
            return BadRequest(result.Conflict);
        return BadRequest(new { message = result.Error ?? "Không thể xác nhận đơn." });
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

    /// <summary>With-driver trip: record (or, after a dispute, correct) the check before handover.</summary>
    [HttpPost("bookings/{id:int}/pre-handover")]
    public async Task<ActionResult<VehicleInspectionResponse>> PreHandover(
        int id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] VehicleConditionRequest? request)
    {
        var (inspection, error) = await dispatchService.SavePreHandoverAsync(id, CurrentUserId(), request);
        return inspection is null ? BadRequest(new { message = error }) : Ok(inspection);
    }

    [HttpGet("inspection-disputes")]
    public async Task<ActionResult<List<InspectionDisputeResponse>>> GetDisputes(
        [FromQuery] int? bookingId, [FromQuery] string? status)
        => Ok(await dispatchService.GetDisputesAsync(bookingId, status));

    [HttpPost("inspection-disputes/{id:int}/resolve")]
    public async Task<ActionResult<InspectionDisputeResponse>> ResolveDispute(
        int id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ResolveDisputeRequest? request)
    {
        var (dispute, error) = await dispatchService.ResolveDisputeAsync(id, CurrentUserId(), request?.Note);
        return dispute is null ? BadRequest(new { message = error }) : Ok(dispute);
    }

    /// <summary>Check a driver's return report: Confirmed, or Mismatch (+ note → incident).</summary>
    [HttpPost("inspections/{id:int}/verify")]
    public async Task<ActionResult<VehicleInspectionResponse>> VerifyReturn(int id, VerifyReturnRequest request)
    {
        var (inspection, error) = await dispatchService.VerifyReturnAsync(id, CurrentUserId(), request.Result, request.Note);
        return inspection is null ? BadRequest(new { message = error }) : Ok(inspection);
    }

    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("incidents")]
    public async Task<ActionResult<List<IncidentResponse>>> GetIncidents(
        [FromQuery] int? bookingId,
        [FromQuery] string? status)
        => Ok(await incidents.GetOperationsAsync(bookingId, status));

    [HttpPost("incidents/{id:int}/resolve")]
    public async Task<ActionResult<IncidentResponse>> ResolveIncident(int id, ResolveIncidentRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (incident, error, status) = await incidents.ResolveAsync(id, userId, request);
        return status switch
        {
            200 => Ok(incident),
            404 => NotFound(new { message = error }),
            _ => BadRequest(new { message = error })
        };
    }

    [HttpGet("inspections")]
    public async Task<ActionResult<IReadOnlyList<VehicleInspectionResponse>>> GetInspections(
        [FromQuery] int? bookingId)
        => Ok(await inspections.GetAdminAsync(bookingId));
}
