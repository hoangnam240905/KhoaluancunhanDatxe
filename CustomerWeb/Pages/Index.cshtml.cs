using CustomerWeb.Services;
using CustomerWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CustomerWeb.Pages;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public List<VehicleTypeResponse> VehicleTypes { get; set; } = [];
    public List<VehicleTypeRecommendationResponse> Recommendations { get; set; } = [];
    public bool IsLoggedIn => auth.IsLoggedIn;
    public string? CustomerName => auth.FullName;
    public string? RecommendError { get; set; }
    public bool HasRecommendQuery { get; set; }

    [BindProperty(SupportsGet = true)] public DateTime? StartDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? EndDate { get; set; }
    [BindProperty(SupportsGet = true)] public int? Seats { get; set; }
    [BindProperty(SupportsGet = true)] public decimal? PriceMax { get; set; }
    [BindProperty(SupportsGet = true)] public decimal? EstimatedDistance { get; set; }

    public async Task OnGetAsync()
    {
        VehicleTypes = await api.GetVehicleTypesAsync();
        if (StartDate is null)
            StartDate = DateTime.Now.AddDays(1).Date.AddHours(8);
        if (EndDate is null)
            EndDate = StartDate.Value.AddDays(1);

        HasRecommendQuery = Request.Query.ContainsKey("StartDate") || Request.Query.ContainsKey("startDate");
        if (HasRecommendQuery && StartDate is not null && EndDate is not null)
        {
            var (data, error) = await api.GetRecommendedAsync(
                StartDate.Value, EndDate.Value, Seats, PriceMax, EstimatedDistance);
            Recommendations = data;
            RecommendError = error;
        }
    }
}
