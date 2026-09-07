using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Bookings;
using Backend.Services;
using Backend.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Authorize]
[Route("api/bookings")]
public class BookingsController(
    BookingService bookingService,
    VehicleInspectionService inspections) : ControllerBase
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

        if (role == RoleNames.Customer)
            return booking.CustomerId == userId ? Ok(booking) : Forbid();

        if (role == RoleNames.Driver)
            return booking.Assignment?.DriverId == userId ? Ok(booking) : Forbid();

        if (role is RoleNames.Admin or RoleNames.Dispatcher)
            return Ok(booking);

        return Forbid();
    }

    [HttpGet("{id:int}/inspections")]
    public async Task<ActionResult<IReadOnlyList<VehicleInspectionResponse>>> GetInspections(int id)
    {
        var booking = await bookingService.GetBookingByIdAsync(id);
        if (booking is null) return NotFound();

        var role = User.FindFirstValue(ClaimTypes.Role);
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (role == RoleNames.Driver)
            return Forbid();

        if (role == RoleNames.Customer && booking.CustomerId != userId)
            return Forbid();

        if (role is not (RoleNames.Customer or RoleNames.Admin or RoleNames.Dispatcher))
            return Forbid();

        return Ok(await inspections.GetByBookingAsync(id));
    }

    [Authorize(Roles = RoleNames.Customer)]
    [HttpPost]
    public async Task<ActionResult<BookingResponse>> Create(
        CreateBookingRequest request,
        [FromQuery] bool fromRecommendation = false)
    {
        if (!RentalModes.TryResolve(request.RentalMode, out _))
            return BadRequest(new { message = "Hình thức thuê không hợp lệ." });

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var booking = await bookingService.CreateBookingAsync(userId, request, fromRecommendation);
        return booking is null
            ? BadRequest(new { message = "Dữ liệu đặt xe không hợp lệ." })
            : CreatedAtAction(nameof(GetById), new { id = booking.BookingId }, booking);
    }

    [Authorize(Roles = RoleNames.Customer)]
    [HttpPost("{id:int}/reviews")]
    public async Task<ActionResult<ReviewResponse>> CreateReview(int id, CreateReviewRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (review, error) = await bookingService.CreateReviewAsync(id, userId, request);
        return review is null
            ? BadRequest(new { message = error ?? "Không thể đánh giá đơn này." })
            : Ok(review);
    }

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Dispatcher}")]
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<BookingResponse>> UpdateStatus(int id, UpdateBookingStatusRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (booking, error, status) = await bookingService.UpdateStatusAsync(
            id, request.Status, userId, request.Note);
        return status switch
        {
            StatusCodes.Status200OK when booking is not null => Ok(booking),
            StatusCodes.Status404NotFound => NotFound(new { message = error }),
            _ => BadRequest(new { message = error ?? BookingStateTransitionRules.InvalidTransition })
        };
    }
}
