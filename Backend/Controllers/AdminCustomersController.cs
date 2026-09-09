using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Customers;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/customers")]
public class AdminCustomersController(AdminCustomerService customers) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AdminCustomerListResponse>> GetAll(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await customers.GetAsync(keyword, page, pageSize));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AdminCustomerResponse>> GetById(int id)
    {
        var data = await customers.GetByIdAsync(id);
        return data is null ? NotFound(new { message = "Không tìm thấy khách hàng." }) : Ok(data);
    }

    [HttpPost]
    public async Task<ActionResult<AdminCustomerResponse>> Create(CreateAdminCustomerRequest request)
    {
        var (data, error, status) = await customers.CreateAsync(request);
        if (data is not null)
            return StatusCode(StatusCodes.Status201Created, data);

        return Status(status, error);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AdminCustomerResponse>> Update(int id, UpdateAdminCustomerRequest request)
    {
        var (data, error, status) = await customers.UpdateAsync(id, request);
        if (data is not null)
            return Ok(data);

        return Status(status, error);
    }

    [HttpPut("{id:int}/lock")]
    public async Task<ActionResult<AdminCustomerResponse>> Lock(int id, LockCustomerRequest request)
    {
        var (data, error, status) = await customers.SetLockedAsync(
            id, request.IsLocked, request.Reason, CurrentUserId());
        if (data is not null)
            return Ok(data);

        return Status(status, error);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, [FromBody] DeactivateCustomerRequest? request)
    {
        var (ok, error, status) = await customers.DeactivateAsync(id, request?.Reason, CurrentUserId());
        if (ok)
            return Ok(new { message = "Đã vô hiệu hóa khách hàng." });

        return Status(status, error);
    }

    private int? CurrentUserId()
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private ActionResult Status(int status, string? error) => status switch
    {
        StatusCodes.Status404NotFound => NotFound(new { message = error }),
        _ => BadRequest(new { message = error ?? "Không thể cập nhật khách hàng." })
    };
}
