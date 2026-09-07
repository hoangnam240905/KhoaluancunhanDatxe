using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Vehicles;
using Backend.Services;
using Backend.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/vehicle-types")]
public class VehicleTypesController(VehicleService vehicleService, RecommendationService recommendations) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<List<VehicleTypeResponse>>> GetAll()
        => Ok(await vehicleService.GetVehicleTypesAsync());

    [AllowAnonymous]
    [HttpGet("recommended")]
    public async Task<ActionResult<List<VehicleTypeRecommendationResponse>>> GetRecommended(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int? seats,
        [FromQuery] decimal? priceMax,
        [FromQuery] decimal? estimatedDistance)
    {
        int? customerId = null;
        if (User.Identity?.IsAuthenticated == true
            && User.IsInRole(RoleNames.Customer)
            && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            customerId = userId;

        var (data, error, status) = await recommendations.RecommendAsync(
            startDate, endDate, seats, priceMax, estimatedDistance, customerId);
        return data is null
            ? StatusCode(status, new { message = error })
            : Ok(data);
    }
}

[ApiController]
[Route("api/vehicles")]
public class VehiclesController(VehicleService vehicleService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<List<VehicleResponse>>> GetAll([FromQuery] string? status)
        => Ok(await vehicleService.GetVehiclesAsync(status));

    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<VehicleResponse>> GetById(int id)
    {
        var vehicle = await vehicleService.GetVehicleByIdAsync(id);
        return vehicle is null ? NotFound() : Ok(vehicle);
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost]
    public async Task<ActionResult<VehicleResponse>> Create(CreateVehicleRequest request)
    {
        var (vehicle, error, status) = await vehicleService.CreateVehicleAsync(request);
        if (vehicle is null)
            return StatusCode(status, new { message = error });
        return CreatedAtAction(nameof(GetById), new { id = vehicle.VehicleId }, vehicle);
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<VehicleResponse>> Update(int id, UpdateVehicleRequest request)
    {
        var (vehicle, error, status) = await vehicleService.UpdateVehicleAsync(id, request);
        if (vehicle is null)
            return StatusCode(status, new { message = error });
        return Ok(vehicle);
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var (deleted, error, status) = await vehicleService.DeleteVehicleAsync(id);
        if (deleted)
            return NoContent();
        return StatusCode(status, new { message = error ?? VehicleOccupancyRules.CannotDelete });
    }
}
