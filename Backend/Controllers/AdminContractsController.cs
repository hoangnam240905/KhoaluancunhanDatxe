using Backend.Constants;
using Backend.DTOs.Contracts;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/contracts")]
public class AdminContractsController(ContractService contracts) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ContractResponse>>> GetAll(
        [FromQuery] int? bookingId,
        [FromQuery] string? status)
        => Ok(await contracts.GetAdminAsync(bookingId, status));
}
