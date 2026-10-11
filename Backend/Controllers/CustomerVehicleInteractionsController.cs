using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Interactions;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Authorize]
[Route("api/customer-vehicle-interactions")]
public class CustomerVehicleInteractionsController(CustomerVehicleInteractionService interactions) : ControllerBase
{
    [Authorize(Roles = RoleNames.Customer)]
    [HttpPost]
    public async Task<ActionResult<CustomerVehicleInteractionResponse>> Create(
        CreateCustomerVehicleInteractionRequest request)
    {
        var customerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (data, error, status) = await interactions.CreateAsync(customerId, request);
        return status switch
        {
            StatusCodes.Status201Created when data is not null => StatusCode(StatusCodes.Status201Created, data),
            StatusCodes.Status404NotFound => NotFound(new { message = error }),
            _ => BadRequest(new { message = error })
        };
    }

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Customer}")]
    [HttpGet("customer/{customerId:int}")]
    public async Task<ActionResult<List<CustomerVehicleInteractionResponse>>> GetByCustomer(
        int customerId, [FromQuery] string? interactionType = null)
    {
        if (User.IsInRole(RoleNames.Customer)
            && int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!) != customerId)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Không có quyền xem tương tác này." });

        var (data, error, status) = await interactions.GetByCustomerAsync(customerId, interactionType);
        return status == StatusCodes.Status404NotFound
            ? NotFound(new { message = error })
            : Ok(data);
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpGet("vehicle/{vehicleId:int}")]
    public async Task<ActionResult<List<CustomerVehicleInteractionResponse>>> GetByVehicle(
        int vehicleId, [FromQuery] string? interactionType = null)
    {
        var (data, error, status) = await interactions.GetByVehicleAsync(vehicleId, interactionType);
        return status == StatusCodes.Status404NotFound
            ? NotFound(new { message = error })
            : Ok(data);
    }
}
