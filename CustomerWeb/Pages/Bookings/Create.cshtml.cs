using System.ComponentModel.DataAnnotations;
using CustomerWeb.Models;
using CustomerWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CustomerWeb.Pages.Bookings;

public class CreateModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty] public bool QuoteConfirmed { get; set; }
    [BindProperty] public string? QuotedFingerprint { get; set; }

    public List<VehicleTypeResponse> VehicleTypes { get; set; } = [];
    public BookingQuoteResponse? Quote { get; set; }
    public string? ErrorMessage { get; set; }
    public string? InfoMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Vui lòng chọn loại xe.")]
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn loại xe.")]
        public int VehicleTypeId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn hình thức thuê.")]
        public string RentalMode { get; set; } = "WithDriver";

        [Required(ErrorMessage = "Vui lòng nhập điểm đón.")]
        public string PickupAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập điểm trả.")]
        public string DropoffAddress { get; set; } = string.Empty;

        [Required, DataType(DataType.DateTime)]
        public DateTime StartDate { get; set; } = DateTime.Now.AddDays(1);

        [Required, DataType(DataType.DateTime)]
        public DateTime EndDate { get; set; } = DateTime.Now.AddDays(1).AddHours(8);

        public decimal? EstimatedDistance { get; set; }
        public string? Notes { get; set; }
    }

    public string CurrentFingerprint =>
        $"{Input.VehicleTypeId}|{Input.RentalMode}|{Input.StartDate:yyyy-MM-ddTHH:mm}|{Input.EndDate:yyyy-MM-ddTHH:mm}|{Input.EstimatedDistance}";

    public static bool IsSelfDrive(string? mode) => mode == "SelfDrive";

    public async Task<IActionResult> OnGetAsync(int? typeId)
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");

        VehicleTypes = await api.GetVehicleTypesAsync();
        if (string.IsNullOrWhiteSpace(Input.RentalMode))
            Input.RentalMode = "WithDriver";
        if (typeId.HasValue) Input.VehicleTypeId = typeId.Value;
        else if (VehicleTypes.Count > 0) Input.VehicleTypeId = VehicleTypes[0].TypeId;

        return Page();
    }

    public async Task<IActionResult> OnPostQuoteAsync()
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        VehicleTypes = await api.GetVehicleTypesAsync();
        NormalizeAndValidate();
        if (!ModelState.IsValid) return Page();
        await LoadQuoteAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");

        VehicleTypes = await api.GetVehicleTypesAsync();
        NormalizeAndValidate();
        if (!ModelState.IsValid) return Page();

        if (!QuoteConfirmed || QuotedFingerprint != CurrentFingerprint)
        {
            await LoadQuoteAsync();
            if (Quote is not null)
                InfoMessage = "Đã lấy báo giá từ máy chủ. Kiểm tra rồi bấm đặt xe.";
            return Page();
        }

        var (data, error) = await api.CreateBookingAsync(new CreateBookingRequest(
            Input.VehicleTypeId,
            Input.PickupAddress,
            Input.DropoffAddress,
            null, null, null, null,
            Input.StartDate,
            Input.EndDate,
            Input.EstimatedDistance,
            Input.Notes,
            Input.RentalMode));

        if (data is null)
        {
            ErrorMessage = error;
            await LoadQuoteAsync();
            return Page();
        }

        return RedirectToPage("Details", new { id = data.BookingId });
    }

    private void NormalizeAndValidate()
    {
        if (Input.RentalMode is not ("WithDriver" or "SelfDrive"))
            Input.RentalMode = "WithDriver";

        if (Input.EndDate <= Input.StartDate)
            ModelState.AddModelError("Input.EndDate", "Thời gian kết thúc phải sau thời gian bắt đầu.");

        if (Input.VehicleTypeId <= 0 || VehicleTypes.All(t => t.TypeId != Input.VehicleTypeId))
            ModelState.AddModelError("Input.VehicleTypeId", "Vui lòng chọn loại xe.");
    }

    private async Task LoadQuoteAsync()
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
            QuoteConfirmed = false;
            QuotedFingerprint = null;
            ErrorMessage = error;
            return;
        }

        Quote = quote;
        QuoteConfirmed = true;
        QuotedFingerprint = CurrentFingerprint;
        ErrorMessage = null;
    }
}
