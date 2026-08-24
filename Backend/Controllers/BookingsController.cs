using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Bookings;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Authorize]
[Route("api/bookings")]
public class BookingsController(BookingService bookingService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<BookingResponse>>> GetAll([FromQuery] string? status)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (role == RoleNames.Customer)
            return Ok(await bookingService.GetBookingsAsync(customerId: userId, status: status));

        if (role is RoleNames.Admin or RoleNames.Dispatcher)
            return Ok(await bookingService.GetBookingsAsync(status: status));

        return Forbid();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BookingResponse>> GetById(int id)
    {
        var booking = await bookingService.GetBookingByIdAsync(id);
        if (booking is null) return NotFound();

        var role = User.FindFirstValue(ClaimTypes.Role);
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (role == RoleNames.Customer && booking.CustomerId != userId)
            return Forbid();

        return Ok(booking);
    }

    [Authorize(Roles = RoleNames.Customer)]
    [HttpPost]
    public async Task<ActionResult<BookingResponse>> Create(CreateBookingRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var booking = await bookingService.CreateBookingAsync(userId, request);
        return booking is null
            ? BadRequest(new { message = "Du lieu dat xe khong hop le." })
            : CreatedAtAction(nameof(GetById), new { id = booking.BookingId }, booking);
    }

    [Authorize(Roles = RoleNames.Customer)]
    [HttpPost("{id:int}/reviews")]
    public async Task<ActionResult<ReviewResponse>> CreateReview(int id, CreateReviewRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var review = await bookingService.CreateReviewAsync(id, userId, request);
        return review is null ? BadRequest(new { message = "Khong the danh gia don nay." }) : Ok(review);
    }

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Dispatcher}")]
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<BookingResponse>> UpdateStatus(int id, UpdateBookingStatusRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var booking = await bookingService.UpdateStatusAsync(id, request.Status, userId, request.Note);
        return booking is null ? NotFound() : Ok(booking);
    }
}
