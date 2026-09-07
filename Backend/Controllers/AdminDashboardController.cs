using Backend.Constants;
using Backend.DTOs.Dashboard;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/dashboard")]
public class AdminDashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var (data, error) = await dashboard.GetAsync(from, to);
        return data is null
            ? BadRequest(new { message = error ?? "Không thể tải dashboard." })
            : Ok(data);
    }
}
