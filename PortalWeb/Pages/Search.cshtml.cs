using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PortalWeb.Display;
using PortalWeb.Models;
using PortalWeb.Services;

namespace PortalWeb.Pages;

public class SearchModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public bool IsLoggedIn => auth.IsLoggedIn;
    public bool IsCustomer => auth.Role == "Customer";
    public string? UserName => auth.FullName;

    [BindProperty(SupportsGet = true)] public DateTime? StartDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? EndDate { get; set; }
    [BindProperty(SupportsGet = true)] public List<int> TypeId { get; set; } = [];
    [BindProperty(SupportsGet = true)] public List<int> Seats { get; set; } = [];
    [BindProperty(SupportsGet = true)] public decimal? PriceMax { get; set; }

    public IReadOnlyList<VehicleTypeResponse> VehicleTypes { get; private set; } = [];
    public IReadOnlyList<SearchVehicleCard> Results { get; private set; } = [];
    public IReadOnlyList<CustomerNotice> Notices { get; private set; } = [];
    public string? ErrorMessage { get; private set; }

    public int? HeroTypeId => TypeId.Count == 1 ? TypeId[0] : null;
    public int? HeroSeats => VehicleSearchSupport.SeatMin(Seats);

    public async Task<IActionResult> OnGetAsync()
    {
        if (auth.IsLoggedIn && auth.Role is not null and not "Customer")
            return Redirect(RoleRoutes.HomeFor(auth.Role));

        if (StartDate is null)
            StartDate = VehicleSearchSupport.DefaultStart();
        if (EndDate is null)
            EndDate = VehicleSearchSupport.DefaultEnd(StartDate.Value);

        VehicleTypes = await api.GetVehicleTypesAsync();
        var (results, error) = await VehicleSearchSupport.LoadSearchAsync(
            api, VehicleTypes, TypeId, Seats, PriceMax, StartDate, EndDate);
        ErrorMessage = error;
        Results = results;
        Notices = SearchUi.Notices;
        return Page();
    }
}
