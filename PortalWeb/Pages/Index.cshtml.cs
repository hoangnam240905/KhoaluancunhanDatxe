using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PortalWeb.Pages;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public List<VehicleTypeResponse> VehicleTypes { get; set; } = [];
    public IReadOnlyList<PopularRoute> PopularRoutes => PortalContent.PopularRoutes;
    public IReadOnlyList<FeatureItem> Features => PortalContent.Features;
    public IReadOnlyList<ProcessStep> BookingSteps => PortalContent.BookingSteps;
    public IReadOnlyList<string> FaqItems => PortalContent.FaqItems;
    public bool IsLoggedIn => auth.IsLoggedIn;
    public bool IsCustomer => auth.Role == "Customer";
    public string? UserName => auth.FullName;

    public async Task<IActionResult> OnGetAsync()
    {
        if (auth.IsLoggedIn && auth.Role is not null and not "Customer")
            return Redirect(RoleRoutes.HomeFor(auth.Role));

        VehicleTypes = await api.GetVehicleTypesAsync();
        return Page();
    }
}
