using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Payments;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Customer)]
[Route("api/payments")]
public class PaymentsController(PaymentService paymentService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PaymentResponse>> Create(CreatePaymentRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (payment, error, status) = await paymentService.CreateAsync(userId, request);
        if (payment is not null)
            return Created($"/api/bookings/{payment.BookingId}/payments", payment);

        return status switch
        {
            StatusCodes.Status403Forbidden => Forbid(),
            StatusCodes.Status404NotFound => NotFound(new { message = error }),
            _ => BadRequest(new { message = error ?? "Không thể tạo thanh toán." })
        };
    }

    [HttpGet("/api/bookings/{bookingId:int}/payments")]
    public async Task<ActionResult<List<PaymentResponse>>> GetByBooking(int bookingId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (payments, error, status) = await paymentService.GetByBookingAsync(userId, bookingId);
        if (payments is not null)
            return Ok(payments);

        return status switch
        {
            StatusCodes.Status403Forbidden => Forbid(),
            StatusCodes.Status404NotFound => NotFound(new { message = error }),
            _ => BadRequest(new { message = error ?? "Không thể lấy thanh toán." })
        };
    }

    [HttpPost("{id:int}/simulate-success")]
    public async Task<ActionResult<PaymentResponse>> SimulateSuccess(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (payment, error, status) = await paymentService.SimulateSuccessAsync(userId, id);
        if (payment is not null)
            return Ok(payment);

        return status switch
        {
            StatusCodes.Status403Forbidden => Forbid(),
            StatusCodes.Status404NotFound => NotFound(new { message = error }),
            _ => BadRequest(new { message = error ?? "Không thể mô phỏng thanh toán." })
        };
    }

    [HttpPost("{id:int}/simulate-failure")]
    public async Task<ActionResult<PaymentResponse>> SimulateFailure(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (payment, error, status) = await paymentService.SimulateFailureAsync(userId, id);
        if (payment is not null)
            return Ok(payment);

        return status switch
        {
            StatusCodes.Status403Forbidden => Forbid(),
            StatusCodes.Status404NotFound => NotFound(new { message = error }),
            _ => BadRequest(new { message = error ?? "Không thể mô phỏng thanh toán." })
        };
    }
}
