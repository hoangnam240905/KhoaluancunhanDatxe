using System.ComponentModel.DataAnnotations;
using CustomerWeb.Display;
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
    [BindProperty] public bool FromRecommendation { get; set; }
    [BindProperty(SupportsGet = true)] public string? Slug { get; set; }

    public List<VehicleTypeResponse> VehicleTypes { get; set; } = [];
    public List<VehicleResponse> Vehicles { get; set; } = [];
    public BookingQuoteResponse? Quote { get; set; }
    public BookingResponse? CreatedBooking { get; set; }
    public string? ErrorMessage { get; set; }
    public string? InfoMessage { get; set; }
    public bool IsLoggedIn => auth.IsLoggedIn;
    public string? CustomerName => auth.FullName;
    public CatalogVehicle SelectedVehicle { get; private set; } = CreateBookingUi.Unspecified();
    public bool SameReturnPlace { get; private set; } = true;

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
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
        public DateTime StartDate { get; set; } = CreateBookingUi.DefaultPickup;

        [Display(Name = "Ngày kết thúc")]
        [Required(ErrorMessage = "Vui lòng chọn thời gian kết thúc.")]
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
        public DateTime EndDate { get; set; } = CreateBookingUi.DefaultReturn;

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

    public async Task<IActionResult> OnGetAsync(int? typeId, DateTime? start, DateTime? end, decimal? distance, bool fromRecommendation = false, int? vehicleId = null)
    {
        if (!auth.IsLoggedIn) return CustomerLoginRedirect.ToLogin(this);

        await LoadLookupsAsync();
        if (vehicleId is > 0)
            Input.VehicleId = vehicleId;
        await EnsureSelectedVehicleLoadedAsync();
        ApplyIncoming(typeId, start, end, distance, fromRecommendation, pickup: null, dropoff: null, vehicleId);
        await LoadQuoteIfReadyAsync();
        return Page();
    }

    public async Task<IActionResult> OnGetQuoteJsonAsync(
        int vehicleTypeId,
        DateTime startDate,
        DateTime endDate,
        string? rentalMode,
        decimal? estimatedDistance)
    {
        if (!auth.IsLoggedIn) return Unauthorized();

        Input.VehicleTypeId = vehicleTypeId;
        Input.StartDate = startDate;
        Input.EndDate = endDate;
        Input.RentalMode = rentalMode ?? "WithDriver";
        Input.EstimatedDistance = estimatedDistance;

        var (quote, error) = await api.GetQuoteAsync(
            Input.VehicleTypeId,
            Input.StartDate,
            Input.EndDate,
            Input.RentalMode,
            Input.EstimatedDistance);
        if (quote is null)
            return BadRequest(new { message = BookingSubmitUi.FriendlyQuoteFailure(error) });

        return new JsonResult(quote);
    }

    public async Task<IActionResult> OnPostQuoteAsync()
    {
        if (!auth.IsLoggedIn) return CustomerLoginRedirect.ToLogin(this);
        ErrorMessage = null;
        InfoMessage = null;
        await LoadLookupsAsync();
        await EnsureSelectedVehicleLoadedAsync();
        NormalizeAndValidate();
        ResolveSelection(Input.VehicleTypeId);
        if (!ModelState.IsValid)
        {
            ApplyFirstModelError();
            return Page();
        }
        await LoadQuoteAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!auth.IsLoggedIn) return CustomerLoginRedirect.ToLogin(this);

        ErrorMessage = null;
        InfoMessage = null;
        await LoadLookupsAsync();
        await EnsureSelectedVehicleLoadedAsync();
        NormalizeAndValidate();
        ResolveSelection(Input.VehicleTypeId);
        if (!ModelState.IsValid)
        {
            ApplyFirstModelError();
            return Page();
        }

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
                Input.PickupAddress.Trim(),
                Input.DropoffAddress.Trim(),
                null, null, null, null,
                Input.StartDate,
                Input.EndDate,
                Input.EstimatedDistance,
                string.IsNullOrWhiteSpace(Input.Notes) ? null : Input.Notes.Trim(),
                Input.RentalMode,
                Input.VehicleId is > 0 ? Input.VehicleId : null), FromRecommendation);
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

        return RedirectToPage("Details", new { id = data.BookingId });
    }

    private void ApplyIncoming(
        int? typeId, DateTime? start, DateTime? end, decimal? distance,
        bool fromRecommendation, string? pickup, string? dropoff, int? vehicleId)
    {
        if (string.IsNullOrWhiteSpace(Input.RentalMode))
            Input.RentalMode = "WithDriver";

        if (vehicleId is > 0)
            Input.VehicleId = vehicleId;

        ResolveSelection(typeId);
        if (SelectedVehicle.VehicleId is int selectedId and > 0)
        {
            Input.VehicleId = selectedId;
            var row = Vehicles.FirstOrDefault(v => v.VehicleId == selectedId);
            Input.VehicleTypeId = row?.TypeId
                ?? CreateBookingUi.ResolveTypeId(SelectedVehicle, typeId, VehicleTypes);
        }
        else
        {
            Input.VehicleTypeId = CreateBookingUi.ResolveTypeId(SelectedVehicle, typeId, VehicleTypes);
        }

        if (start.HasValue) Input.StartDate = start.Value;
        else if (Input.StartDate == default) Input.StartDate = CreateBookingUi.DefaultPickup;
        if (end.HasValue) Input.EndDate = end.Value;
        else if (Input.EndDate == default) Input.EndDate = CreateBookingUi.DefaultReturn;
        EnsureDatesNotPast();

        if (distance.HasValue) Input.EstimatedDistance = distance;
        FromRecommendation = fromRecommendation;

        if (!string.IsNullOrWhiteSpace(pickup)) Input.PickupAddress = pickup;
        if (!string.IsNullOrWhiteSpace(dropoff)) Input.DropoffAddress = dropoff;
        if (string.IsNullOrWhiteSpace(Input.PickupAddress))
            Input.PickupAddress = CreateBookingUi.DefaultPickupPlace;
        if (string.IsNullOrWhiteSpace(Input.DropoffAddress))
            Input.DropoffAddress = Input.PickupAddress;

        SameReturnPlace = string.Equals(
            Input.PickupAddress.Trim(),
            Input.DropoffAddress.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureDatesNotPast()
    {
        if (Input.StartDate.Date >= DateTime.Now.Date) return;
        Input.StartDate = DateTime.Now.AddDays(1).Date.AddHours(10);
        if (Input.EndDate <= Input.StartDate)
            Input.EndDate = Input.StartDate.AddDays(3);
    }

    private void NormalizeAndValidate()
    {
        if (Input.RentalMode is not ("WithDriver" or "SelfDrive"))
            Input.RentalMode = "WithDriver";

        Input.PickupAddress = Input.PickupAddress?.Trim() ?? string.Empty;
        Input.DropoffAddress = Input.DropoffAddress?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(Input.DropoffAddress))
            Input.DropoffAddress = Input.PickupAddress;
        SameReturnPlace = string.Equals(
            Input.PickupAddress,
            Input.DropoffAddress,
            StringComparison.OrdinalIgnoreCase);

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

        if (Input.VehicleId is <= 0)
            Input.VehicleId = null;
    }

    private void ApplyFirstModelError()
    {
        ErrorMessage = ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
    }

    private async Task LoadQuoteIfReadyAsync()
    {
        if (Input.VehicleTypeId <= 0 || Input.EndDate <= Input.StartDate)
        {
            if (Input.VehicleTypeId <= 0)
                ErrorMessage = BookingSubmitUi.QuoteUnavailable;
            return;
        }

        await LoadQuoteAsync();
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
                ErrorMessage = BookingSubmitUi.FriendlyQuoteFailure(error);
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

    private async Task EnsureSelectedVehicleLoadedAsync()
    {
        var requested = CreateBookingUi.ParseVehicleId(Slug ?? Request.Query["slug"], Input.VehicleId);
        if (requested is not int id || Vehicles.Any(v => v.VehicleId == id))
            return;
        var extra = await api.GetVehicleByIdAsync(id);
        if (extra is not null)
            Vehicles.Add(extra);
    }

    private void ResolveSelection(int? typeId)
    {
        var requested = CreateBookingUi.ParseVehicleId(Slug ?? Request.Query["slug"], Input.VehicleId);
        if (requested is int id)
        {
            var row = Vehicles.FirstOrDefault(v => v.VehicleId == id);
            if (row is not null)
            {
                SelectedVehicle = CreateBookingUi.FromApi(
                    row, VehicleTypes.FirstOrDefault(t => t.TypeId == row.TypeId));
                Input.VehicleId = row.VehicleId;
                return;
            }
        }

        var unique = CreateBookingUi.TryMapUniqueBrandModel(
            Slug ?? Request.Query["slug"], Vehicles, VehicleTypes);
        if (unique is not null)
        {
            SelectedVehicle = unique;
            Input.VehicleId = unique.VehicleId;
            return;
        }

        SelectedVehicle = CreateBookingUi.Resolve(Slug ?? Request.Query["slug"], typeId, VehicleTypes);
    }
}
