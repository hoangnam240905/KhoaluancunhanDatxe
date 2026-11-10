using System.Security.Claims;
using Backend.Constants;
using Backend.DTOs.Customers;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>Customer self-service profile for V7 recommendation fields (JWT only — no IDOR).</summary>
[ApiController]
[Authorize(Roles = RoleNames.Customer)]
[Route("api/customers")]
public class CustomersController(CustomerProfileService profiles, CustomerLicenseService licences) : ControllerBase
{
    /// <summary>My self-drive licence: status, photos and which vehicle types I may drive (until endDate if given).</summary>
    [HttpGet("me/license")]
    public async Task<ActionResult<CustomerLicenseResponse>> MyLicense([FromQuery] DateTime? endDate)
    {
        var data = await licences.GetAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), endDate);
        return data is null ? NotFound(new { message = CustomerLicenseService.NotFound }) : Ok(data);
    }

    [HttpPut("me/license")]
    public async Task<ActionResult<CustomerLicenseResponse>> UpdateMyLicense(UpdateCustomerLicenseRequest request)
    {
        var (data, error) = await licences.UpdateAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), request);
        return data is null ? BadRequest(new { message = error }) : Ok(data);
    }

    [HttpGet("me")]
    public async Task<ActionResult<CustomerProfileResponse>> GetMe()
    {
        var customerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (data, error, status) = await profiles.GetOwnAsync(customerId);
        return data is null
            ? StatusCode(status, new { message = error })
            : Ok(data);
    }

    [HttpPut("me")]
    public async Task<ActionResult<CustomerProfileResponse>> UpdateMe(UpdateCustomerProfileRequest request)
    {
        var customerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (data, error, status) = await profiles.UpdateOwnAsync(customerId, request);
        return data is null
            ? StatusCode(status, new { message = error })
            : Ok(data);
    }
}
