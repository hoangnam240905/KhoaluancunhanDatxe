using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Bookings;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        var booking = await dispatchService.ConfirmBookingAsync(id, userId);
        return booking is null ? BadRequest(new { message = "Không thể xác nhận đơn." }) : Ok(booking);
    }

    [HttpPost("bookings/{id:int}/assign")]
    public async Task<ActionResult<BookingResponse>> Assign(int id, AssignTripRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var booking = await dispatchService.AssignTripAsync(id, request, userId);
        return booking is null ? BadRequest(new { message = "Không thể phân công chuyến đi." }) : Ok(booking);
    }
}
