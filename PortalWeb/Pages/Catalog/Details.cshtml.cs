using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PortalWeb.Display;
using PortalWeb.Services;

namespace PortalWeb.Pages.Catalog;

public class DetailsModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public bool IsLoggedIn => auth.IsLoggedIn;
    public bool IsCustomer => auth.Role == "Customer";
    public string? UserName => auth.FullName;

    [BindProperty(SupportsGet = true)] public DateTime? StartDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? EndDate { get; set; }

    public CatalogVehicle? Vehicle { get; private set; }
    public IReadOnlyList<CatalogRateTier> RateTiers { get; private set; } = [];
    public IReadOnlyList<CatalogReview> Reviews { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        if (auth.IsLoggedIn && auth.Role is not null and not "Customer")
            return Redirect(RoleRoutes.HomeFor(auth.Role));

        var types = await api.GetVehicleTypesAsync();
        var vehicles = await api.GetVehiclesAsync();

        if (int.TryParse(slug, out var vehicleId) && vehicleId > 0)
        {
            var row = await api.GetVehicleAsync(vehicleId)
                      ?? vehicles.FirstOrDefault(v => v.VehicleId == vehicleId);
            if (row is null)
                return NotFound();
            Vehicle = CreateBookingUi.FromApi(row, types.FirstOrDefault(t => t.TypeId == row.TypeId));
        }
        else
        {
            Vehicle = CreateBookingUi.TryMapUniqueBrandModel(slug, vehicles, types);
            if (Vehicle is null)
                return NotFound();
        }

        RateTiers = CatalogUi.RateTiers(Vehicle.PricePerDay);
        Reviews = [];
        return Page();
    }

    public async Task<IActionResult> OnGetBusyPeriodsAsync(string slug, DateTime? from, DateTime? to)
    {
        if (auth.IsLoggedIn && auth.Role is not null and not "Customer")
            return Redirect(RoleRoutes.HomeFor(auth.Role));

        var vehicleId = await ResolveVehicleIdAsync(slug);
        if (vehicleId is null)
            return new JsonResult(new { message = "Không tìm thấy xe." }) { StatusCode = 404 };

        var (data, status, error) = await api.GetVehicleBusyPeriodsAsync(vehicleId.Value, from, to);
        if (data is null)
            return new JsonResult(new { message = error }) { StatusCode = status };
        return new JsonResult(data);
    }

    private async Task<int?> ResolveVehicleIdAsync(string slug)
    {
        if (int.TryParse(slug, out var vehicleId) && vehicleId > 0)
            return vehicleId;

        var types = await api.GetVehicleTypesAsync();
        var vehicles = await api.GetVehiclesAsync();
        return CreateBookingUi.TryMapUniqueBrandModel(slug, vehicles, types)?.VehicleId;
    }
}
