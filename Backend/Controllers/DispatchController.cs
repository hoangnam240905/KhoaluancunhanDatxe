using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Bookings;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Backend.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Dispatcher)]
[Route("api/dispatch")]
public class DispatchController(DispatchService dispatchService) : ControllerBase
{
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
        var (booking, error) = await dispatchService.AssignTripAsync(id, request, userId);
        return booking is null ? BadRequest(new { message = error ?? "Không thể phân công chuyến đi." }) : Ok(booking);
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
}
