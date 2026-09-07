using Backend.Constants;
using Backend.DTOs.Incidents;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/incidents")]
public class AdminIncidentsController(IncidentService incidents) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<IncidentResponse>>> GetAll(
        [FromQuery] int? bookingId,
        [FromQuery] string? status)
        => Ok(await incidents.GetOperationsAsync(bookingId, status));
}
