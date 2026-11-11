using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Documents;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>Licence / ID-card photos (plan1.md Phần F). Files are served only through this controller.</summary>
[ApiController]
[Authorize]
[Route("api/documents")]
public class DocumentsController(UserDocumentService documents, DriverVerificationService driverVerification, CustomerLicenseService customerLicences) : ControllerBase
{
    /// <summary>Upload one side of a document (multipart: type, file).</summary>
    [HttpPost]
    [Authorize(Roles = $"{RoleNames.Driver},{RoleNames.Customer}")]
    [RequestSizeLimit(UserDocumentService.MaxBytes + 256 * 1024)]
    public async Task<ActionResult<UserDocumentResponse>> Upload([FromForm] string? type, IFormFile? file, [FromForm] int? licenseId)
    {
        await using var stream = file?.OpenReadStream();
        var (document, error) = await documents.UploadAsync(CurrentUserId(), type, stream, file?.Length ?? 0, licenseId);
        if (document is null)
            return BadRequest(new { message = error });
        if (User.IsInRole(RoleNames.Driver))
            await driverVerification.NotifyDispatchersIfReadyAsync(CurrentUserId());
        else if (User.IsInRole(RoleNames.Customer))
            await customerLicences.NotifyDispatchersIfReadyAsync(CurrentUserId());
        return Ok(document);
    }

    /// <summary>My newest photo of each type, with its review status.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<List<UserDocumentResponse>>> Mine()
        => Ok(await documents.GetCurrentAsync(CurrentUserId()));

    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Dispatcher},{RoleNames.Admin}")]
    public async Task<ActionResult<List<UserDocumentResponse>>> List([FromQuery] int? userId, [FromQuery] string? status)
        => Ok(await documents.ListAsync(userId, status));

    [HttpGet("{id:int}/file")]
    public async Task<IActionResult> File(int id)
    {
        var (path, contentType, status) = await documents.GetFileAsync(
            id, CurrentUserId(), User.FindFirstValue(ClaimTypes.Role));
        return status switch
        {
            200 => PhysicalFile(path!, contentType!),
            403 => Forbid(),
            _ => NotFound(new { message = UserDocumentService.NotFound })
        };
    }

    [HttpPost("{id:int}/review")]
    [Authorize(Roles = $"{RoleNames.Dispatcher},{RoleNames.Admin}")]
    public async Task<ActionResult<UserDocumentResponse>> Review(int id, ReviewDocumentRequest request)
    {
        var (document, error, status) = await documents.ReviewAsync(id, CurrentUserId(), request.Approve, request.Reason);
        return status switch
        {
            200 => Ok(document),
            404 => NotFound(new { message = error }),
            _ => BadRequest(new { message = error })
        };
    }

    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
