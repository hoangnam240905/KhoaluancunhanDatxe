using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Vehicles;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>The logged-in customer's favourite cars. CustomerId comes from the JWT only.</summary>
[ApiController]
[Authorize(Roles = RoleNames.Customer)]
[Route("api/favorites")]
public class FavoritesController(FavoriteVehicleService favorites) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<VehicleResponse>>> Get()
        => Ok(await favorites.GetAsync(CustomerId()));

    [HttpPut("{vehicleId:int}")]
    public async Task<IActionResult> Add(int vehicleId)
        => await favorites.AddAsync(CustomerId(), vehicleId)
            ? NoContent()
            : NotFound(new { message = "Không tìm thấy xe." });

    [HttpDelete("{vehicleId:int}")]
    public async Task<IActionResult> Remove(int vehicleId)
    {
        await favorites.RemoveAsync(CustomerId(), vehicleId);
        return NoContent();
    }

    private int CustomerId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
