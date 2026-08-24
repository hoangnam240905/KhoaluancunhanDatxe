using CustomerWeb.Services;
using CustomerWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CustomerWeb.Pages;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public List<VehicleTypeResponse> VehicleTypes { get; set; } = [];
    public bool IsLoggedIn => auth.IsLoggedIn;
    public string? CustomerName => auth.FullName;

    public async Task OnGetAsync()
    {
        VehicleTypes = await api.GetVehicleTypesAsync();
    }
}
