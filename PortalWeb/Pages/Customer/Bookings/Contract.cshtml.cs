using PortalWeb.Display;
using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Customer.Bookings;

public class ContractModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public BookingResponse? Booking { get; set; }
    public ContractResponse? Contract { get; set; }
    public string? UserName => auth.FullName;
    public ContractView? Presentation { get; private set; }
    public string ViewState { get; private set; } = "missing";
    public int BookingRouteId { get; private set; }
    public string? ErrorMessage { get; set; }
    public bool AccessDenied { get; private set; }
    public bool NotFoundBooking { get; private set; }
    public bool NeedsLogin { get; private set; }
    public bool CanIssue { get; private set; }
    public bool CanSign { get; private set; }

    [BindProperty] public bool Agreed { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;
        await LoadAsync(id);
        if (NeedsLogin) return RedirectToPage("/Account/Login");
        return Page();
    }

    public async Task<IActionResult> OnPostIssueAsync(int id)
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;
        await LoadAsync(id);
        if (NeedsLogin) return RedirectToPage("/Account/Login");
        if (AccessDenied || NotFoundBooking || Booking is null)
            return Page();

        if (!CanIssue)
        {
            ErrorMessage = Booking.Status == "Cancelled"
                ? ContractUi.CancelledNoIssue
                : BookingDetailUi.IsAwaitingDispatcher(Booking.Status)
                    ? ContractUi.WaitingBody
                    : "Không thể lập hợp đồng cho đơn này.";
            return Page();
        }

        var (created, error) = await api.CreateContractAsync(id);
        if (created is null)
        {
            ErrorMessage = ContractUi.PresentError(error);
            await LoadAsync(id);
            return Page();
        }

        await LoadAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostSignAsync(int id)
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;
        await LoadAsync(id);
        if (NeedsLogin) return RedirectToPage("/Account/Login");
        if (AccessDenied || NotFoundBooking || Booking is null)
            return Page();

        if (string.Equals(Contract?.Status, "Signed", StringComparison.OrdinalIgnoreCase))
            return Page();

        if (!CanSign || Contract is null)
        {
            ErrorMessage = ContractUi.CannotSign;
            return Page();
        }

        if (!Agreed)
        {
            ErrorMessage = ContractUi.SignNeedAgree;
            return Page();
        }

        var (signed, error) = await api.SimulateSignContractAsync(Contract.ContractId);
        if (signed is null)
        {
            ErrorMessage = ContractUi.PresentError(error);
            await LoadAsync(id);
            return Page();
        }

        await LoadAsync(id);
        return Page();
    }

    private async Task LoadAsync(int id)
    {
        BookingRouteId = id;
        CanIssue = false;
        CanSign = false;
        try
        {
            var (data, status) = await api.GetBookingWithStatusAsync(id);
            if (status is 401)
            {
                NeedsLogin = true;
                return;
            }

            if (status is 403)
            {
                AccessDenied = true;
                ErrorMessage = ContractUi.AccessDenied;
                return;
            }

            if (data is null)
            {
                NotFoundBooking = true;
                ErrorMessage = status >= 500 ? ContractUi.LoadFailure : ContractUi.NotFound;
                return;
            }

            Booking = data;
            if (BookingDetailUi.IsAwaitingDispatcher(data.Status))
            {
                Presentation = ContractUi.From(data, null);
                ViewState = "waiting";
                CanIssue = false;
                CanSign = false;
                return;
            }

            var (contract, contractStatus) = await api.GetContractWithStatusAsync(id);
            if (contractStatus is 403)
            {
                AccessDenied = true;
                Booking = null;
                ErrorMessage = ContractUi.AccessDenied;
                return;
            }

            if (contractStatus >= 500)
            {
                ErrorMessage = ContractUi.LoadFailure;
                return;
            }

            Contract = contract;
            Presentation = ContractUi.From(data, contract);
            ViewState = ContractUi.ViewState(contract?.Status);
            CanIssue = contract is null && BookingDetailUi.AllowsDeposit(data.Status);
            CanSign = contract is not null
                      && string.Equals(contract.Status, "Issued", StringComparison.OrdinalIgnoreCase)
                      && BookingDetailUi.AllowsDeposit(data.Status);
        }
        catch
        {
            ErrorMessage = ContractUi.LoadFailure;
        }
    }
}
