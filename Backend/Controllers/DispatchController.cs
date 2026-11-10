using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Bookings;
using Backend.DTOs.Dispatch;
using Backend.DTOs.Incidents;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Backend.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Dispatcher)]
[Route("api/dispatch")]
public class DispatchController(
    DispatchService dispatchService,
    IncidentService incidents,
    VehicleInspectionService inspections,
    DriverVerificationService driverVerification,
    CustomerLicenseService customerLicences,
    VehicleChangeService vehicleChanges) : ControllerBase
{
    // ---- Self-drive customer licences (plan1.md Phần E) ----

    /// <summary>status: Pending (default) / Approved / Rejected / All.</summary>
    [HttpGet("customer-licenses")]
    public async Task<ActionResult<List<Backend.DTOs.Customers.CustomerLicenseResponse>>> CustomerLicenses([FromQuery] string? status)
        => Ok(await customerLicences.ListAsync(status));

    [HttpGet("customer-licenses/{customerId:int}")]
    public async Task<ActionResult<Backend.DTOs.Customers.CustomerLicenseResponse>> CustomerLicense(int customerId)
    {
        var data = await customerLicences.GetAsync(customerId);
        return data is null ? NotFound(new { message = CustomerLicenseService.NotFound }) : Ok(data);
    }

    [HttpPost("customer-licenses/{customerId:int}/review")]
    public async Task<ActionResult<Backend.DTOs.Customers.CustomerLicenseResponse>> ReviewCustomerLicense(
        int customerId, Backend.DTOs.Customers.ReviewCustomerLicenseRequest request)
    {
        var (data, error) = await customerLicences.ReviewAsync(customerId, CurrentUserId(), request.Approve, request.Reason);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    // ---- Driver licence verification (plan1.md Phần D) ----

    /// <summary>status: Pending (default) / Verified / Rejected / All.</summary>
    [HttpGet("driver-verifications")]
    public async Task<ActionResult<List<Backend.DTOs.Drivers.DriverVerificationResponse>>> DriverVerifications([FromQuery] string? status)
        => Ok(await driverVerification.ListAsync(status));

    [HttpGet("driver-verifications/{driverId:int}")]
    public async Task<ActionResult<Backend.DTOs.Drivers.DriverVerificationResponse>> DriverVerification(int driverId)
    {
        var data = await driverVerification.GetAsync(driverId);
        return data is null ? NotFound(new { message = DriverVerificationService.NotFound }) : Ok(data);
    }

    /// <summary>Step 1: accept the photos (driver is asked to come to the office) or reject them with a reason.</summary>
    [HttpPost("driver-verifications/{driverId:int}/photos")]
    public async Task<ActionResult<Backend.DTOs.Drivers.DriverVerificationResponse>> ReviewDriverPhotos(
        int driverId, Backend.DTOs.Drivers.ReviewDriverPhotosRequest request)
    {
        var (data, error) = await driverVerification.ReviewPhotosAsync(driverId, CurrentUserId(), request.Accept, request.Reason);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    /// <summary>Step 2: the original licence was checked at the office — verified, or not (reason required).</summary>
    [HttpPost("driver-verifications/{driverId:int}/verify")]
    public async Task<ActionResult<Backend.DTOs.Drivers.DriverVerificationResponse>> VerifyDriver(
        int driverId, Backend.DTOs.Drivers.VerifyDriverRequest request)
    {
        var (data, error) = await driverVerification.VerifyAsync(driverId, CurrentUserId(), request.Verified, request.Reason);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    [HttpGet("fleet-status")]
    public async Task<ActionResult<DispatchFleetStatusResponse>> FleetStatus()
        => Ok(await dispatchService.GetFleetStatusAsync());

    [HttpGet("bookings/{id:int}/assignable")]
    public async Task<ActionResult<DispatchAssignableResponse>> Assignable(int id)
    {
        var (data, error) = await dispatchService.GetAssignableAsync(id);
        return data is null ? BadRequest(new { message = error ?? "Không thể lấy danh sách khả dụng." }) : Ok(data);
    }

    [HttpPost("bookings/{id:int}/confirm")]
    public async Task<ActionResult<BookingResponse>> Confirm(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await dispatchService.ConfirmBookingAsync(id, userId);
        if (result.Booking is not null)
            return Ok(result.Booking);
        if (result.Conflict is not null)
            return BadRequest(result.Conflict);
        return BadRequest(new { message = result.Error ?? "Không thể xác nhận đơn." });
    }

    [HttpPost("bookings/{id:int}/assign")]
    public async Task<ActionResult<BookingResponse>> Assign(int id, AssignTripRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await dispatchService.AssignTripAsync(id, request, userId);
        if (result.Booking is not null)
            return Ok(result.Booking);
        if (result.Conflict is not null)
            return BadRequest(result.Conflict);
        return BadRequest(new { message = result.Error ?? "Không thể phân công chuyến đi." });
    }

    [HttpPost("bookings/{id:int}/handover")]
    public async Task<ActionResult<BookingResponse>> Handover(
        int id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] VehicleConditionRequest? request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (booking, error) = await dispatchService.HandoverSelfDriveAsync(id, userId, request);
        return booking is null ? BadRequest(new { message = error ?? "Không thể giao xe." }) : Ok(booking);
    }

    [HttpPost("bookings/{id:int}/complete")]
    public async Task<ActionResult<BookingResponse>> Complete(
        int id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] VehicleConditionRequest? request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (booking, error) = await dispatchService.CompleteSelfDriveAsync(id, userId, request);
        return booking is null ? BadRequest(new { message = error ?? "Không thể hoàn thành đơn." }) : Ok(booking);
    }

    /// <summary>With-driver trip: record (or, after a dispute, correct) the check before handover.</summary>
    [HttpPost("bookings/{id:int}/pre-handover")]
    public async Task<ActionResult<VehicleInspectionResponse>> PreHandover(
        int id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] VehicleConditionRequest? request)
    {
        var (inspection, error) = await dispatchService.SavePreHandoverAsync(id, CurrentUserId(), request);
        return inspection is null ? BadRequest(new { message = error }) : Ok(inspection);
    }

    [HttpGet("inspection-disputes")]
    public async Task<ActionResult<List<InspectionDisputeResponse>>> GetDisputes(
        [FromQuery] int? bookingId, [FromQuery] string? status)
        => Ok(await dispatchService.GetDisputesAsync(bookingId, status));

    [HttpPost("inspection-disputes/{id:int}/resolve")]
    public async Task<ActionResult<InspectionDisputeResponse>> ResolveDispute(
        int id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ResolveDisputeRequest? request)
    {
        var (dispute, error) = await dispatchService.ResolveDisputeAsync(id, CurrentUserId(), request?.Note);
        return dispute is null ? BadRequest(new { message = error }) : Ok(dispute);
    }

    /// <summary>Check a driver's return report: Confirmed, or Mismatch (+ note → incident).</summary>
    [HttpPost("inspections/{id:int}/verify")]
    public async Task<ActionResult<VehicleInspectionResponse>> VerifyReturn(int id, VerifyReturnRequest request)
    {
        var (inspection, error) = await dispatchService.VerifyReturnAsync(id, CurrentUserId(), request.Result, request.Note);
        return inspection is null ? BadRequest(new { message = error }) : Ok(inspection);
    }

    // ---- Vehicle change (plan1.md Phần I) ----

    /// <summary>Free cars for the rest of the rental: same type first; other types (with price difference) only when none.</summary>
    [HttpGet("bookings/{id:int}/vehicle-change-options")]
    public async Task<ActionResult<VehicleChangeOptionsResponse>> VehicleChangeOptions(int id)
    {
        var (data, error) = await vehicleChanges.GetOptionsAsync(id);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    [HttpPost("bookings/{id:int}/vehicle-change")]
    public async Task<ActionResult<VehicleChangeResponse>> ChangeVehicle(int id, ChangeVehicleRequest request)
    {
        var (data, error) = await vehicleChanges.ApplyAsync(id, CurrentUserId(), request);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("incidents")]
    public async Task<ActionResult<List<IncidentResponse>>> GetIncidents(
        [FromQuery] int? bookingId,
        [FromQuery] string? status)
        => Ok(await incidents.GetOperationsAsync(bookingId, status));

    [HttpPost("incidents/{id:int}/resolve")]
    public async Task<ActionResult<IncidentResponse>> ResolveIncident(int id, ResolveIncidentRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (incident, error, status) = await incidents.ResolveAsync(id, userId, request);
        return status switch
        {
            200 => Ok(incident),
            404 => NotFound(new { message = error }),
            _ => BadRequest(new { message = error })
        };
    }

    [HttpGet("inspections")]
    public async Task<ActionResult<IReadOnlyList<VehicleInspectionResponse>>> GetInspections(
        [FromQuery] int? bookingId)
        => Ok(await inspections.GetAdminAsync(bookingId));
}
