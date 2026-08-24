using Backend.Constants;
using Backend.DTOs.Vehicles;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/vehicle-types")]
public class VehicleTypesController(VehicleService vehicleService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<List<VehicleTypeResponse>>> GetAll()
        => Ok(await vehicleService.GetVehicleTypesAsync());
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
        var vehicle = await vehicleService.CreateVehicleAsync(request);
        return vehicle is null ? BadRequest() : CreatedAtAction(nameof(GetById), new { id = vehicle.VehicleId }, vehicle);
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<VehicleResponse>> Update(int id, UpdateVehicleRequest request)
    {
        var vehicle = await vehicleService.UpdateVehicleAsync(id, request);
        return vehicle is null ? NotFound() : Ok(vehicle);
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await vehicleService.DeleteVehicleAsync(id);
        return deleted ? NoContent() : BadRequest(new { message = "Khong the xoa xe da duoc su dung." });
    }
}
