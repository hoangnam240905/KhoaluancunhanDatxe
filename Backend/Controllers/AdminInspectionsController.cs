using Backend.Constants;
using Backend.DTOs.Bookings;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/inspections")]
public class AdminInspectionsController(VehicleInspectionService inspections) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VehicleInspectionResponse>>> GetAll([FromQuery] int? bookingId)
        => Ok(await inspections.GetAdminAsync(bookingId));
}
