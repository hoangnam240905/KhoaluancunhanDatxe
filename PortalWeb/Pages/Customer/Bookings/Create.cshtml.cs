using System.ComponentModel.DataAnnotations;
using PortalWeb.Display;
using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Customer.Bookings;

public class CreateModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty] public bool QuoteConfirmed { get; set; }
    [BindProperty] public string? QuotedFingerprint { get; set; }

    public List<VehicleTypeResponse> VehicleTypes { get; set; } = [];
    public List<VehicleResponse> Vehicles { get; set; } = [];
    public BookingQuoteResponse? Quote { get; set; }
    public BookingResponse? CreatedBooking { get; set; }
    public string? ErrorMessage { get; set; }
    public string? InfoMessage { get; set; }

    public class InputModel
    {
        [Display(Name = "Loại xe")]
        [Required(ErrorMessage = "Vui lòng chọn loại xe.")]
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn loại xe.")]
        public int VehicleTypeId { get; set; }

        [Display(Name = "Hình thức thuê")]
        [Required(ErrorMessage = "Vui lòng chọn hình thức thuê.")]
        public string RentalMode { get; set; } = "WithDriver";

        [Display(Name = "Điểm đón")]
        [Required(ErrorMessage = "Vui lòng nhập điểm đón.")]
        public string PickupAddress { get; set; } = string.Empty;

        [Display(Name = "Điểm trả")]
        [Required(ErrorMessage = "Vui lòng nhập điểm trả.")]
        public string DropoffAddress { get; set; } = string.Empty;

        [Display(Name = "Ngày bắt đầu")]
        [Required(ErrorMessage = "Vui lòng chọn thời gian bắt đầu.")]
        public DateTime StartDate { get; set; } = DateTime.Now.AddDays(1);

        [Display(Name = "Ngày kết thúc")]
        [Required(ErrorMessage = "Vui lòng chọn thời gian kết thúc.")]
        public DateTime EndDate { get; set; } = DateTime.Now.AddDays(1).AddHours(8);

        [Display(Name = "Km ước tính")]
        public decimal? EstimatedDistance { get; set; }

        [Display(Name = "Ghi chú")]
        public string? Notes { get; set; }

        [Display(Name = "Xe")]
        public int? VehicleId { get; set; }
    }

    public string CurrentFingerprint =>
        $"{Input.VehicleTypeId}|{Input.RentalMode}|{Input.VehicleId}|{Input.StartDate:yyyy-MM-ddTHH:mm}|{Input.EndDate:yyyy-MM-ddTHH:mm}|{Input.EstimatedDistance}";

    public static bool IsSelfDrive(string? mode) => mode == "SelfDrive";
    public static string RentalModeLabel(string? mode) => mode == "SelfDrive" ? "Tự lái" : "Có tài xế";

    [BindProperty] public bool FromRecommendation { get; set; }

    public async Task<IActionResult> OnGetAsync(
        int? typeId, string? pickup, string? dropoff, decimal? distance,
        DateTime? start, DateTime? end, bool fromRecommendation = false)
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;
        await LoadLookupsAsync();
        if (string.IsNullOrWhiteSpace(Input.RentalMode))
            Input.RentalMode = "WithDriver";
        if (typeId.HasValue) Input.VehicleTypeId = typeId.Value;
        else if (VehicleTypes.Count > 0 && Input.VehicleTypeId == 0)
            Input.VehicleTypeId = VehicleTypes[0].TypeId;
        if (!string.IsNullOrWhiteSpace(pickup)) Input.PickupAddress = pickup;
        if (!string.IsNullOrWhiteSpace(dropoff)) Input.DropoffAddress = dropoff;
        if (distance.HasValue) Input.EstimatedDistance = distance;
        if (start.HasValue) Input.StartDate = start.Value;
        if (end.HasValue) Input.EndDate = end.Value;
        FromRecommendation = fromRecommendation;
        return Page();
    }

    public async Task<IActionResult> OnPostQuoteAsync()
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;
        ErrorMessage = null;
        InfoMessage = null;
        await LoadLookupsAsync();
        NormalizeAndValidate();
        if (!ModelState.IsValid) return Page();

        await LoadQuoteAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;
        ErrorMessage = null;
        InfoMessage = null;
        await LoadLookupsAsync();
        NormalizeAndValidate();
        if (!ModelState.IsValid) return Page();

        if (!QuoteConfirmed || QuotedFingerprint != CurrentFingerprint)
        {
            await LoadQuoteAsync();
            if (Quote is not null)
                InfoMessage = BookingSubmitUi.NeedQuoteReview;
            return Page();
        }

        BookingResponse? data = null;
        string? error = null;
        try
        {
            (data, error) = await api.CreateBookingAsync(new CreateBookingRequest(
                Input.VehicleTypeId,
                Input.PickupAddress,
                Input.DropoffAddress,
                null, null, null, null,
                Input.StartDate,
                Input.EndDate,
                Input.EstimatedDistance,
                Input.Notes,
                Input.RentalMode,
                Input.VehicleId), FromRecommendation);
        }
        catch
        {
            data = null;
            error = null;
        }

        if (data is null || data.BookingId <= 0)
        {
            ErrorMessage = BookingSubmitUi.FriendlyCreateFailure(error);
            await LoadQuoteAsync(overwriteError: false);
            return Page();
        }

        CreatedBooking = data;
        return Page();
    }

    private void NormalizeAndValidate()
    {
        if (Input.RentalMode is not ("WithDriver" or "SelfDrive"))
            Input.RentalMode = "WithDriver";

        if (Input.EndDate <= Input.StartDate)
            ModelState.AddModelError("Input.EndDate", "Thời gian kết thúc phải sau thời gian bắt đầu.");

        if (Input.StartDate.Date < DateTime.Now.Date)
            ModelState.AddModelError("Input.StartDate", "Ngày bắt đầu không được trong quá khứ.");

        if (Input.VehicleTypeId <= 0 || VehicleTypes.All(t => t.TypeId != Input.VehicleTypeId))
            ModelState.AddModelError("Input.VehicleTypeId", "Vui lòng chọn loại xe.");

        if (Input.VehicleId is int vehicleId and > 0
            && Vehicles.All(v => v.VehicleId != vehicleId || v.TypeId != Input.VehicleTypeId))
            ModelState.AddModelError("Input.VehicleId", "Xe không thuộc loại xe đã chọn hoặc không còn khả dụng.");

        if (Input.EstimatedDistance is < 0)
            ModelState.AddModelError("Input.EstimatedDistance", "Km ước tính không được âm.");
    }

    private async Task LoadQuoteAsync(bool overwriteError = true)
    {
        var (quote, error) = await api.GetQuoteAsync(
            Input.VehicleTypeId,
            Input.StartDate,
            Input.EndDate,
            Input.RentalMode,
            Input.EstimatedDistance);
        if (quote is null)
        {
            Quote = null;
            ApplyQuoteConfirmation(false, null);
            if (overwriteError || string.IsNullOrEmpty(ErrorMessage))
                ErrorMessage = error;
            return;
        }

        Quote = quote;
        ApplyQuoteConfirmation(true, CurrentFingerprint);
        if (overwriteError)
            ErrorMessage = null;
    }

    private void ApplyQuoteConfirmation(bool confirmed, string? fingerprint)
    {
        QuoteConfirmed = confirmed;
        QuotedFingerprint = fingerprint;
        ModelState.Remove(nameof(QuoteConfirmed));
        ModelState.Remove(nameof(QuotedFingerprint));
    }

    private async Task LoadLookupsAsync()
    {
        VehicleTypes = await api.GetVehicleTypesAsync();
        Vehicles = (await api.GetVehiclesAsync("Available"))
            .Where(v => v.Status == "Available")
            .ToList();
    }
}
