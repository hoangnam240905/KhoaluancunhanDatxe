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
    /// <summary>"WithDriver" (default) or "SelfDrive"; carried to the booking form.</summary>
    [BindProperty(SupportsGet = true)] public string RentalMode { get; set; } = "WithDriver";

    public bool IsSelfDrive => RentalMode == "SelfDrive";

    public IReadOnlyList<VehicleTypeResponse> VehicleTypes { get; private set; } = [];
    public IReadOnlyList<SearchVehicleCard> Results { get; private set; } = [];
    public string? ErrorMessage { get; private set; }

    /// <summary>Self-drive (plan1.md Phần E): KH-0 message when the customer has no usable licence yet.</summary>
    public string? LicenceMessage { get; private set; }
    /// <summary>Self-drive: types the licence does not cover, with the reason (shown above the results).</summary>
    public List<SelfDriveTypeEligibility> ExcludedTypes { get; private set; } = [];
    /// <summary>Self-drive without an approved/declared licence: cars are listed but cannot be booked yet.</summary>
    public bool BookingBlocked { get; private set; }

    public int? HeroTypeId => TypeId.Count == 1 ? TypeId[0] : null;
    public int? HeroSeats => VehicleSearchSupport.SeatMin(Seats);

    public async Task<IActionResult> OnGetAsync()
    {
        if (auth.IsLoggedIn && auth.Role is not null and not "Customer")
            return Redirect(RoleRoutes.HomeFor(auth.Role));

        if (RentalMode is not ("SelfDrive" or "WithDriver"))
            RentalMode = "WithDriver";
        if (StartDate is null)
            StartDate = VehicleSearchSupport.DefaultStart();
        if (EndDate is null)
            EndDate = VehicleSearchSupport.DefaultEnd(StartDate.Value);

        VehicleTypes = await api.GetVehicleTypesAsync();
        var (results, error) = await VehicleSearchSupport.LoadSearchAsync(
            api, VehicleTypes, TypeId, Seats, PriceMax, StartDate, EndDate);
        ErrorMessage = error;
        Results = results;
        if (IsSelfDrive && IsCustomer)
            await ApplyLicenceAsync();
        return Page();
    }

    private async Task ApplyLicenceAsync()
    {
        var licence = await api.GetMyLicenseAsync(EndDate);
        if (licence is null || licence.LicenseStatus is "None" or "Rejected")
        {
            LicenceMessage = licence?.LicenseStatus == "Rejected"
                ? $"Bằng lái chưa được chấp nhận{(string.IsNullOrWhiteSpace(licence.ReviewNote) ? "" : $" (lý do: {licence.ReviewNote})")}. Vui lòng gửi lại ảnh bằng lái hoặc liên hệ điều phối."
                : "Hãy cung cấp bằng lái để xem các loại xe bạn được phép lái.";
            BookingBlocked = true;
            return;
        }
        ExcludedTypes = licence.VehicleTypes.Where(t => !t.CanDrive).ToList();
        var blockedSeats = ExcludedTypes.Select(t => t.SeatCapacity).ToHashSet();
        Results = Results.Where(r => !blockedSeats.Contains(r.Seats)).ToList();
        if (licence.LicenseStatus == "Pending")
            LicenceMessage = "Bằng lái đang chờ điều phối duyệt. Bạn vẫn đặt xe được; đơn tự lái sẽ được xác nhận sau khi bằng lái được duyệt.";
    }
}
