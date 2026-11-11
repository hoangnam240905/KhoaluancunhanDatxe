using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Bookings;
using Backend.DTOs.Dispatch;
using Backend.DTOs.Drivers;
using Backend.DTOs.Incidents;
using Backend.Services;
using Backend.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Backend.Controllers;

[ApiController]
[Route("api/drivers")]
public class DriversController(
    DriverService driverService,
    IncidentService incidentService,
    DriverEligibilityService driverEligibility,
    DriverVerificationService verification) : ControllerBase
{
    /// <summary>Licence classes the app/web offers (Luật 36/2024 + old licences still valid).</summary>
    [AllowAnonymous]
    [HttpGet("license-classes")]
    public ActionResult<IReadOnlyList<LicenseClassOption>> LicenseClasses()
        => Ok(DriverVerificationService.ClassOptions());

    /// <summary>The signed-in driver's verification file: licence, photos and both review steps.</summary>
    [Authorize(Roles = RoleNames.Driver)]
    [HttpGet("me/verification")]
    public async Task<ActionResult<DriverVerificationResponse>> MyVerification()
    {
        var data = await verification.GetAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!));
        return data is null ? NotFound(new { message = DriverVerificationService.NotFound }) : Ok(data);
    }

    /// <summary>The driver enters / corrects the licence (photos go to POST /api/documents).</summary>
    [Authorize(Roles = RoleNames.Driver)]
    [HttpPut("me/license")]
    public async Task<ActionResult<DriverVerificationResponse>> UpdateMyLicense(UpdateDriverLicenseRequest request)
    {
        var (data, error) = await verification.UpdateLicenseAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), request);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    /// <summary>Phase 9: add another licence (e.g. D1 next to B); photos go to POST /api/documents with licenseId.</summary>
    [Authorize(Roles = RoleNames.Driver)]
    [HttpPost("me/licenses")]
    public async Task<ActionResult<DriverVerificationResponse>> AddMyLicence(UpdateDriverLicenseRequest request)
    {
        var (data, error) = await verification.AddLicenceAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), request);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    /// <summary>Phase 9: correct one licence; only that licence is verified again.</summary>
    [Authorize(Roles = RoleNames.Driver)]
    [HttpPut("me/licenses/{licenseId:int}")]
    public async Task<ActionResult<DriverVerificationResponse>> UpdateMyLicence(int licenseId, UpdateDriverLicenseRequest request)
    {
        var (data, error) = await verification.UpdateLicenceAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), licenseId, request);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Dispatcher}")]
    [HttpGet]
    public async Task<ActionResult<List<DriverResponse>>> GetAll([FromQuery] string? status)
        => Ok(await driverService.GetDriversAsync(status));

    [Authorize(Roles = $"{RoleNames.Customer},{RoleNames.Dispatcher},{RoleNames.Admin}")]
    [HttpGet("available")]
    public async Task<ActionResult<IReadOnlyList<CustomerDriverOptionResponse>>> GetAvailable(
        [FromQuery] int? vehicleId,
        [FromQuery] int? vehicleTypeId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        if (BookingDateRules.ValidateNewRental(startDate, endDate) is string dateError)
            return BadRequest(new { message = dateError });
        if (vehicleId is null && vehicleTypeId is null)
            return BadRequest(new { message = "Vui lòng chọn xe hoặc loại xe." });

        return Ok(await driverEligibility.ListSelectableAsync(vehicleId, vehicleTypeId, startDate, endDate));
    }

    [Authorize(Roles = $"{RoleNames.Customer},{RoleNames.Dispatcher},{RoleNames.Admin}")]
    [HttpGet("{id:int}/schedule")]
    public async Task<ActionResult<DriverScheduleResponse>> GetSchedule(
        int id,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        if (endDate <= startDate)
            return BadRequest(new { message = "Thời gian kết thúc phải sau thời gian bắt đầu." });

        var schedule = await driverEligibility.GetScheduleAsync(id, startDate, endDate);
        return schedule is null ? NotFound() : Ok(schedule);
    }

    [Authorize(Roles = RoleNames.Driver)]
    [HttpGet("me")]
    public async Task<ActionResult<DriverResponse>> GetMe()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var driver = await driverService.GetDriverAsync(userId);
        return driver is null ? NotFound() : Ok(driver);
    }

    [Authorize(Roles = RoleNames.Driver)]
    [HttpGet("me/trips")]
    public async Task<ActionResult<List<BookingResponse>>> GetMyTrips()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(await driverService.GetDriverTripsAsync(userId));
    }

    [Authorize(Roles = RoleNames.Driver)]
    [HttpPatch("me/status")]
    public async Task<ActionResult<DriverResponse>> UpdateMyStatus(UpdateDriverStatusRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (driver, error) = await driverService.UpdateStatusAsync(userId, request.Status);
        return driver is null ? BadRequest(new { message = error ?? "Không thể cập nhật trạng thái." }) : Ok(driver);
    }

    [Authorize(Roles = RoleNames.Driver)]
    [HttpPost("trips/{assignmentId:int}/accept")]
    public async Task<IActionResult> AcceptTrip(int assignmentId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ok = await driverService.AcceptTripAsync(userId, assignmentId);
        return ok ? Ok(new { message = "Đã nhận chuyến." }) : BadRequest();
    }

    [Authorize(Roles = RoleNames.Driver)]
    [HttpPost("trips/{assignmentId:int}/start")]
    public async Task<IActionResult> StartTrip(
        int assignmentId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] VehicleConditionRequest? request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (ok, error) = await driverService.StartTripAsync(userId, assignmentId, request);
        return ok
            ? Ok(new { message = request is null ? "Đã bắt đầu chuyến." : "Đã lưu biên bản đầu chuyến và bắt đầu chuyến." })
            : BadRequest(new { message = error ?? "Không thể bắt đầu chuyến." });
    }

    /// <summary>"Báo sai lệch": the dispatcher's pre-handover record does not match the real car.</summary>
    [Authorize(Roles = RoleNames.Driver)]
    [HttpPost("trips/{assignmentId:int}/dispute")]
    public async Task<IActionResult> RaiseDispute(int assignmentId, RaiseDisputeRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (dispute, error) = await driverService.RaiseDisputeAsync(userId, assignmentId, request.Note);
        return dispute is null
            ? BadRequest(new { message = error })
            : Ok(new { message = "Đã báo sai lệch cho điều phối.", disputeId = dispute.InspectionDisputeId });
    }

    [Authorize(Roles = RoleNames.Driver)]
    [HttpGet("trips/{assignmentId:int}/disputes")]
    public async Task<ActionResult<List<InspectionDisputeResponse>>> MyTripDisputes(int assignmentId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var list = await driverService.GetDisputesAsync(userId, assignmentId);
        return list is null ? NotFound(new { message = "Không tìm thấy chuyến của tài xế này." }) : Ok(list);
    }

    /// <summary>"Nhắc điều phối": no pre-handover record yet.</summary>
    [Authorize(Roles = RoleNames.Driver)]
    [HttpPost("trips/{assignmentId:int}/remind-dispatcher")]
    public async Task<IActionResult> RemindDispatcher(int assignmentId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var error = await driverService.RemindDispatcherAsync(userId, assignmentId);
        return error is null ? Ok(new { message = "Đã nhắc điều phối." }) : BadRequest(new { message = error });
    }

    [Authorize(Roles = RoleNames.Driver)]
    [HttpPost("trips/{assignmentId:int}/complete")]
    public async Task<IActionResult> CompleteTrip(
        int assignmentId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] VehicleConditionRequest? request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (ok, error) = await driverService.CompleteTripAsync(userId, assignmentId, request);
        if (ok)
            return Ok(new { message = "Đã hoàn thành chuyến." });
        return error is null ? BadRequest() : BadRequest(new { message = error });
    }

    [Authorize(Roles = RoleNames.Driver)]
    [HttpGet("me/incidents")]
    public async Task<ActionResult<List<IncidentResponse>>> GetMyIncidents()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (incidents, _, _) = await incidentService.GetByDriverAsync(userId);
        return Ok(incidents);
    }

    [Authorize(Roles = RoleNames.Driver)]
    [HttpPost("trips/{assignmentId:int}/incidents")]
    public async Task<ActionResult<IncidentResponse>> ReportIncident(int assignmentId, CreateIncidentRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (incident, error, status) = await incidentService.CreateAsync(userId, assignmentId, request);
        if (incident is not null)
            return Created($"/api/drivers/me/incidents", incident);

        return status switch
        {
            StatusCodes.Status403Forbidden => Forbid(),
            StatusCodes.Status404NotFound => NotFound(new { message = error }),
            _ => BadRequest(new { message = error ?? "Không thể báo sự cố." })
        };
    }
}
