using Microsoft.AspNetCore.Mvc;
using PortalWeb.Display;
using PortalWeb.Models;
using PortalWeb.Services;

namespace PortalWeb.Pages.Customer;

/// <summary>
/// "Xe yêu thích" page plus the JSON handlers behind the heart button (favorites.js). A cross-site form
/// cannot send the X-Requested-With header Toggle requires, so no antiforgery token is needed.
/// </summary>
[IgnoreAntiforgeryToken]
public class FavoritesModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public string? UserName => auth.FullName;
    public IReadOnlyList<FavoriteCard> Cards { get; private set; } = [];
    public string? ErrorMessage { get; private set; }

    public sealed record FavoriteCard(CatalogVehicle Vehicle, bool Inactive);

    public async Task<IActionResult> OnGetAsync()
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;

        var vehicles = await api.GetFavoritesAsync();
        if (vehicles is null)
        {
            ErrorMessage = "Không tải được danh sách xe yêu thích.";
            return Page();
        }
        var types = await api.GetVehicleTypesAsync();
        Cards = vehicles
            .Select(v => new FavoriteCard(
                CreateBookingUi.FromApi(v, types.FirstOrDefault(t => t.TypeId == v.TypeId)),
                v.Status == "Inactive"))
            .ToList();
        return Page();
    }

    public async Task<IActionResult> OnGetIdsAsync()
    {
        if (RequireRole(auth, "Customer") is not null) return Unauthorized();
        var vehicles = await api.GetFavoritesAsync();
        return new JsonResult(new { ok = vehicles is not null, ids = vehicles?.Select(v => v.VehicleId) ?? [] });
    }

    public async Task<IActionResult> OnPostToggleAsync(int vehicleId, bool on)
    {
        if (Request.Headers.XRequestedWith != "XMLHttpRequest") return BadRequest();
        if (RequireRole(auth, "Customer") is not null) return Unauthorized();
        return await api.SetFavoriteAsync(vehicleId, on)
            ? new JsonResult(new { ok = true })
            : new JsonResult(new { ok = false }) { StatusCode = 502 };
    }
}
