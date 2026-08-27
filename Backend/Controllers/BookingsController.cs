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

    [Authorize(Roles = RoleNames.Customer)]
    [HttpGet("quote")]
    public async Task<ActionResult<BookingQuoteResponse>> Quote(
        [FromQuery] int? vehicleTypeId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] decimal? estimatedDistance,
        [FromQuery] string? rentalMode)
    {
        if (vehicleTypeId is null or <= 0)
            return BadRequest(new { message = "Loại xe không hợp lệ." });

        if (startDate is null || endDate is null)
            return BadRequest(new { message = "Thời gian thuê không hợp lệ." });

        var (quote, error) = await bookingService.GetQuoteAsync(
            vehicleTypeId.Value,
            startDate.Value,
            endDate.Value,
            estimatedDistance,
            rentalMode);

        return quote is null
            ? BadRequest(new { message = error ?? "Dữ liệu báo giá không hợp lệ." })
            : Ok(quote);
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
        if (!RentalModes.TryResolve(request.RentalMode, out _))
            return BadRequest(new { message = "Hình thức thuê không hợp lệ." });

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var booking = await bookingService.CreateBookingAsync(userId, request);
        return booking is null
            ? BadRequest(new { message = "Dữ liệu đặt xe không hợp lệ." })
            : CreatedAtAction(nameof(GetById), new { id = booking.BookingId }, booking);
    }

    [Authorize(Roles = RoleNames.Customer)]
    [HttpPost("{id:int}/reviews")]
    public async Task<ActionResult<ReviewResponse>> CreateReview(int id, CreateReviewRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var review = await bookingService.CreateReviewAsync(id, userId, request);
        return review is null ? BadRequest(new { message = "Không thể đánh giá đơn này." }) : Ok(review);
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
