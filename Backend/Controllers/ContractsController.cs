using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Contracts;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class ContractsController(ContractService contracts) : ControllerBase
{
    [Authorize(Roles = RoleNames.Customer)]
    [HttpPost("bookings/{bookingId:int}/contract")]
    public async Task<ActionResult<ContractResponse>> Create(int bookingId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (contract, created, error, status) = await contracts.CreateAsync(userId, bookingId);
        if (contract is not null)
            return created
                ? Created($"/api/bookings/{contract.BookingId}/contract", contract)
                : Ok(contract);

        return Status(status, error);
    }

    [HttpGet("bookings/{bookingId:int}/contract")]
    public async Task<ActionResult<ContractResponse>> GetByBooking(int bookingId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = User.FindFirstValue(ClaimTypes.Role);
        var (contract, error, status) = await contracts.GetByBookingAsync(userId, role, bookingId);
        if (contract is not null)
            return Ok(contract);

        return Status(status, error);
    }

    [Authorize(Roles = RoleNames.Customer)]
    [HttpPost("contracts/{id:int}/simulate-sign")]
    public async Task<ActionResult<ContractResponse>> SimulateSign(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (contract, error, status) = await contracts.SimulateSignAsync(userId, id);
        if (contract is not null)
            return Ok(contract);

        return Status(status, error);
    }

    private ActionResult Status(int status, string? error) => status switch
    {
        StatusCodes.Status403Forbidden => Forbid(),
        StatusCodes.Status404NotFound => NotFound(new { message = error }),
        _ => BadRequest(new { message = error ?? "Không thể xử lý hợp đồng." })
    };
}
