using Backend.Constants;
using Backend.DTOs.Vehicles;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/vehicles")]
public class VehicleImagesController(VehicleImageService images) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("{vehicleId:int}/images")]
    public async Task<ActionResult<List<VehicleImageResponse>>> GetByVehicle(int vehicleId)
    {
        var (data, error, status) = await images.GetByVehicleAsync(vehicleId);
        return status == StatusCodes.Status404NotFound
            ? NotFound(new { message = error })
            : Ok(data);
    }

    [AllowAnonymous]
    [HttpGet("{vehicleId:int}/images/{imageId:int}")]
    public async Task<ActionResult<VehicleImageResponse>> GetById(int vehicleId, int imageId)
    {
        var (data, error, status) = await images.GetByIdAsync(vehicleId, imageId);
        return status == StatusCodes.Status404NotFound
            ? NotFound(new { message = error })
            : Ok(data);
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost("{vehicleId:int}/images")]
    public async Task<ActionResult<VehicleImageResponse>> Create(int vehicleId, CreateVehicleImageRequest request)
    {
        var (data, error, status) = await images.CreateAsync(vehicleId, request);
        return status switch
        {
            StatusCodes.Status201Created when data is not null
                => CreatedAtAction(nameof(GetById), new { vehicleId, imageId = data.VehicleImageId }, data),
            StatusCodes.Status404NotFound => NotFound(new { message = error }),
            _ => BadRequest(new { message = error })
        };
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPut("{vehicleId:int}/images/{imageId:int}")]
    public async Task<ActionResult<VehicleImageResponse>> Update(
        int vehicleId, int imageId, UpdateVehicleImageRequest request)
    {
        var (data, error, status) = await images.UpdateAsync(vehicleId, imageId, request);
        return status switch
        {
            StatusCodes.Status200OK when data is not null => Ok(data),
            StatusCodes.Status404NotFound => NotFound(new { message = error }),
            _ => BadRequest(new { message = error })
        };
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpDelete("{vehicleId:int}/images/{imageId:int}")]
    public async Task<IActionResult> Delete(int vehicleId, int imageId)
    {
        var (ok, error, status) = await images.DeleteAsync(vehicleId, imageId);
        return status switch
        {
            StatusCodes.Status204NoContent => NoContent(),
            StatusCodes.Status404NotFound => NotFound(new { message = error }),
            _ => BadRequest(new { message = error })
        };
    }
}
