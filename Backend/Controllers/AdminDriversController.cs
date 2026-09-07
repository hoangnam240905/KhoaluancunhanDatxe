using Backend.Constants;
using Backend.DTOs.Drivers;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/drivers")]
public class AdminDriversController(DriverService driverService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AdminDriverResponse>>> GetAll()
        => Ok(await driverService.GetAdminDriversAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AdminDriverResponse>> GetById(int id)
    {
        var driver = await driverService.GetAdminDriverAsync(id);
        return driver is null ? NotFound(new { message = "Không tìm thấy tài xế." }) : Ok(driver);
    }

    [HttpPost]
    public async Task<ActionResult<AdminDriverResponse>> Create(CreateAdminDriverRequest request)
    {
        var (data, error, status) = await driverService.CreateAdminDriverAsync(request);
        if (data is not null)
            return StatusCode(StatusCodes.Status201Created, data);

        return status == StatusCodes.Status404NotFound
            ? NotFound(new { message = error })
            : BadRequest(new { message = error ?? "Không thể tạo tài xế." });
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AdminDriverResponse>> Update(int id, UpdateAdminDriverRequest request)
    {
        var (data, error, status) = await driverService.UpdateAdminDriverAsync(id, request);
        if (data is not null)
            return Ok(data);

        return status == StatusCodes.Status404NotFound
            ? NotFound(new { message = error })
            : BadRequest(new { message = error ?? "Không thể cập nhật tài xế." });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var (ok, error, status) = await driverService.SoftDeleteAsync(id);
        if (ok)
            return Ok(new { message = "Đã khóa tài xế." });

        return status == StatusCodes.Status404NotFound
            ? NotFound(new { message = error })
            : BadRequest(new { message = error ?? "Không thể xóa tài xế." });
    }
}
