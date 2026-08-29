using Backend.Constants;
using Backend.DTOs.Payments;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/payments")]
public class AdminPaymentsController(PaymentService paymentService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PaymentResponse>>> GetAll(
        [FromQuery] int? bookingId,
        [FromQuery] string? status,
        [FromQuery] string? paymentType)
    {
        var (payments, error, code) = await paymentService.GetAdminAsync(bookingId, status, paymentType);
        if (payments is not null)
            return Ok(payments);

        return code == StatusCodes.Status404NotFound
            ? NotFound(new { message = error })
            : BadRequest(new { message = error ?? "Không thể lấy thanh toán." });
    }
}
