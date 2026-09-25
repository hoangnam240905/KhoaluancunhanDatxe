using PortalWeb.Display;
using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Admin.Vehicles;

public class DetailsModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public VehicleOperationalProfileResponse? Profile { get; set; }
    public string? ErrorMessage { get; set; }
    public bool LoadingFailed { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;

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
