using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Notifications;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController(NotificationService notifications) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<NotificationResponse>>> GetMine()
        => Ok(await notifications.GetMineAsync(CurrentUserId()));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<NotificationResponse>> GetById(int id)
    {
        var (data, error, status) = await notifications.GetByIdAsync(id, CurrentUserId());
        return Status(data, error, status);
    }

    [HttpPatch("{id:int}/read")]
    public async Task<ActionResult<NotificationResponse>> MarkRead(int id)
    {
        var (data, error, status) = await notifications.MarkReadAsync(id, CurrentUserId());
        return Status(data, error, status);
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost]
    public async Task<ActionResult<NotificationResponse>> Create(CreateNotificationRequest request)
    {
        var (data, error, status) = await notifications.CreateAsync(request);
        return status switch
        {
            StatusCodes.Status201Created when data is not null => StatusCode(StatusCodes.Status201Created, data),
            StatusCodes.Status404NotFound => NotFound(new { message = error }),
            _ => BadRequest(new { message = error })
        };
    }

    private int CurrentUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private ActionResult Status(NotificationResponse? data, string? error, int status) => status switch
    {
        StatusCodes.Status200OK when data is not null => Ok(data),
        StatusCodes.Status404NotFound => NotFound(new { message = error }),
        StatusCodes.Status403Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = error }),
        _ => BadRequest(new { message = error })
    };
}
