using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Admin.Contracts;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public List<ContractResponse> Contracts { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public int? FilterBookingId { get; set; }
    public string? FilterStatus { get; set; }

    public async Task<IActionResult> OnGetAsync(int? bookingId, string? status)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
        FilterBookingId = bookingId;
        FilterStatus = status;
        var (data, error) = await api.GetAdminContractsAsync(bookingId, status);
        if (data is null)
        {
            ErrorMessage = error;
            Contracts = [];
            return Page();
        }
        Contracts = data;
        return Page();
    }
}
