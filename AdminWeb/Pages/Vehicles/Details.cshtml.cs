using AdminWeb.Display;
using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminWeb.Pages.Vehicles;

public class DetailsModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public VehicleOperationalProfileResponse? Profile { get; set; }
    public string? ErrorMessage { get; set; }
    public bool LoadingFailed { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        try
        {
            var (data, error) = await api.GetVehicleOperationalProfileAsync(id);
            if (data is null)
            {
                LoadingFailed = true;
                ErrorMessage = error ?? "⚠️ Không thể tải thông tin xe.";
                return Page();
            }

            Profile = data;
            return Page();
        }
        catch (HttpRequestException)
        {
            LoadingFailed = true;
            ErrorMessage = UiDisplay.ApiFailure("⚠️ Không thể tải thông tin xe.", "Không kết nối được máy chủ.");
            return Page();
        }
        catch (TaskCanceledException)
        {
            LoadingFailed = true;
            ErrorMessage = UiDisplay.ApiFailure("⚠️ Không thể tải thông tin xe.", "Hết thời gian chờ máy chủ.");
            return Page();
        }
    }
}
