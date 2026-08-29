using Backend.Constants;
using Backend.DTOs.Vehicles;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/vehicle-types")]
public class AdminVehicleTypesController(VehicleService vehicleService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AdminVehicleTypeResponse>>> GetAll()
        => Ok(await vehicleService.GetAdminVehicleTypesAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AdminVehicleTypeResponse>> GetById(int id)
    {
        var type = await vehicleService.GetAdminVehicleTypeAsync(id);
        return type is null ? NotFound(new { message = "Không tìm thấy loại xe." }) : Ok(type);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AdminVehicleTypeResponse>> Update(int id, UpdateVehicleTypePricingRequest request)
    {
        var (data, error, status) = await vehicleService.UpdatePricingAsync(id, request);
        if (data is not null)
            return Ok(data);

        return status switch
        {
            StatusCodes.Status404NotFound => NotFound(new { message = error }),
            _ => BadRequest(new { message = error ?? "Không thể cập nhật giá." })
        };
    }
}
