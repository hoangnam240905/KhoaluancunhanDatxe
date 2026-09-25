using DispatcherWeb.Display;
using DispatcherWeb.Models;
using DispatcherWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DispatcherWeb.Pages;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty(SupportsGet = true)] public string? Mode { get; set; }
    [BindProperty(SupportsGet = true)] public string? From { get; set; }
    [BindProperty(SupportsGet = true)] public string? To { get; set; }
    [BindProperty(SupportsGet = true)] public int? TypeId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Queue { get; set; }

    public List<BookingResponse> AllBookings { get; set; } = [];
    public List<BookingResponse> VisibleBookings { get; set; } = [];
    public List<BookingResponse> NeedsAssignment { get; set; } = [];
    public IReadOnlyList<(int TypeId, string TypeName)> VehicleTypes { get; set; } = [];
    public Dictionary<int, ContractResponse?> Contracts { get; set; } = [];
    public BookingResponse? Selected { get; set; }
    public ContractResponse? SelectedContract { get; set; }
    public DispatchAssignableResponse? Assignable { get; set; }
    public string? AssignableError { get; set; }
    public DispatchFleetStatusResponse Fleet { get; set; } = new([], []);
    public AssignConflictResponse? Conflict { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
    public bool MessageIsSuccess { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");
        ReadAlerts();
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync(int id)
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");
        var (data, error, conflict) = await api.ConfirmBookingAsync(id);
        if (data is not null)
        {
            TempData["HubSuccess"] = "Đơn thuê đã được xác nhận.";
            return RedirectToPage(FilterAnon(id));
        }

        TempData["HubError"] = string.IsNullOrWhiteSpace(error) ? "Không thể xác nhận đơn." : error;
        if (conflict is not null)
            TempData["HubConflict"] = System.Text.Json.JsonSerializer.Serialize(
                conflict, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        return RedirectToPage(FilterAnon(id));
    }

    public async Task<IActionResult> OnPostRejectAsync(int id)
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");
        var (data, error) = await api.CancelBookingAsync(id, "Điều phối không duyệt / hủy đơn");
        if (data is not null)
            TempData["HubSuccess"] = "Đơn thuê đã được hủy.";
        else
            TempData["HubError"] = string.IsNullOrWhiteSpace(error) ? "Không thể hủy đơn." : error;
        return RedirectToPage(FilterAnon(id));
    }

    public IActionResult OnGetHandover(int id)
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");
        return RedirectToPage("/Handover", new { id });
    }

    public IActionResult OnPostHandover(int id)
    {
        if (!auth.IsLoggedIn) return RedirectToPage("/Account/Login");
        return RedirectToPage("/Handover", new { id });
    }

    public static string RentalModeLabel(string? mode) => UiDisplay.RentalMode(mode);

    public int Count(string? status)
        => string.IsNullOrEmpty(status)
            ? AllBookings.Count
            : AllBookings.Count(b => b.Status == status);

    public Dictionary<string, string> BaseFilters()
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(Q)) data["Q"] = Q;
        if (!string.IsNullOrEmpty(Mode)) data["Mode"] = Mode;
        if (!string.IsNullOrEmpty(From)) data["From"] = From;
        if (!string.IsNullOrEmpty(To)) data["To"] = To;
        if (TypeId is int typeId) data["TypeId"] = typeId.ToString();
        return data;
    }

    public Dictionary<string, string> FilterRoutes(int? id = null)
    {
        var data = BaseFilters();
        if (id is int bookingId) data["id"] = bookingId.ToString();
        if (!string.IsNullOrEmpty(Status)) data["Status"] = Status;
        if (!string.IsNullOrEmpty(Queue)) data["Queue"] = Queue;
        return data;
    }

    private object FilterAnon(int? id) => new { id, Status, Q, Mode, From, To, TypeId, Queue };

    private async Task LoadAsync()
    {
        AllBookings = await api.GetBookingsAsync();
        NeedsAssignment = AllBookings.Where(BookingHubUi.NeedsAssignment).ToList();
        VehicleTypes = AllBookings
            .GroupBy(b => b.VehicleTypeId)
            .Select(g => (g.Key, g.First().VehicleTypeName))
            .OrderBy(x => x.VehicleTypeName)
            .ToList();

        var from = ParseDay(From);
        var to = ParseDay(To);
        var assignQueue = string.Equals(Queue, "assign", StringComparison.OrdinalIgnoreCase);

        VisibleBookings = AllBookings
            .Where(b => assignQueue
                ? b.Status == "Confirmed"
                : string.IsNullOrEmpty(Status) || b.Status == Status)
            .Where(b => string.IsNullOrEmpty(Mode) || b.RentalMode == Mode)
            .Where(b => TypeId is null || b.VehicleTypeId == TypeId)
            .Where(b => BookingHubUi.MatchesSearch(b, Q))
            .Where(b => BookingHubUi.MatchesDates(b, from, to))
            .ToList();

        if (Id is int selectedId)
        {
            Selected = await api.GetBookingAsync(selectedId)
                       ?? AllBookings.FirstOrDefault(b => b.BookingId == selectedId);
            if (Selected is not null && Selected.Status is "Pending" or "Confirmed")
            {
                var (assignable, assignableError) = await api.GetAssignableAsync(selectedId);
                Assignable = assignable;
                AssignableError = assignableError;
            }

            if (Selected is not null)
            {
                var (contract, _, _) = await api.GetContractAsync(selectedId);
                SelectedContract = contract;
                Contracts[selectedId] = contract;
            }
        }

        var missing = VisibleBookings
            .Select(b => b.BookingId)
            .Where(id => !Contracts.ContainsKey(id))
            .Distinct()
            .ToList();
        if (missing.Count > 0)
        {
            var loaded = await Task.WhenAll(missing.Select(async id =>
            {
                var (contract, _, _) = await api.GetContractAsync(id);
                return (id, contract);
            }));
            foreach (var (id, contract) in loaded)
                Contracts[id] = contract;
        }

        var (fleet, _) = await api.GetFleetStatusAsync();
        Fleet = new(fleet?.Vehicles ?? [], fleet?.Drivers ?? []);
    }

    private static DateOnly? ParseDay(string? value)
        => DateOnly.TryParse(value, out var day) ? day : null;

    private void ReadAlerts()
    {
        Message = TempData["HubSuccess"] as string;
        ErrorMessage = TempData["HubError"] as string;
        MessageIsSuccess = !string.IsNullOrEmpty(Message);
        if (TempData["HubConflict"] is string json)
        {
            try
            {
                Conflict = System.Text.Json.JsonSerializer.Deserialize<AssignConflictResponse>(
                    json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* ignore malformed temp payload */ }
        }
    }
}
