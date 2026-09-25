using PortalWeb.Display;
using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Customer.Bookings;

public class PayModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public BookingResponse? Booking { get; set; }
    public List<PaymentResponse> Payments { get; set; } = [];
    public PaymentResponse? Deposit { get; set; }
    public string? UserName => auth.FullName;
    public PaymentView? Presentation { get; private set; }
    public string ViewState { get; private set; } = "form";
    public int BookingRouteId { get; private set; }
    public string? ErrorMessage { get; set; }
    public bool AccessDenied { get; private set; }
    public bool NotFoundBooking { get; private set; }
    public bool NeedsLogin { get; private set; }
    public bool CanSubmit { get; private set; }

    [BindProperty] public string Method { get; set; } = "BankTransfer";

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;
        await LoadAsync(id);
        if (NeedsLogin) return RedirectToPage("/Account/Login");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;
        await LoadAsync(id);
        if (NeedsLogin) return RedirectToPage("/Account/Login");
        if (AccessDenied || NotFoundBooking || Booking is null || Presentation is null)
            return Page();

        Method = PaymentUi.ResolveMethod(Method);

        if (string.Equals(Deposit?.Status, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            ViewState = "success";
            CanSubmit = false;
            return Page();
        }

        if (Booking.Status == "Cancelled")
        {
            ErrorMessage = "Không thể thanh toán cho đơn đã hủy.";
            ViewState = "failed";
            CanSubmit = false;
            return Page();
        }

        if (BookingDetailUi.IsAwaitingDispatcher(Booking.Status))
        {
            ErrorMessage = BookingDetailUi.WaitingBody;
            ViewState = "waiting";
            CanSubmit = false;
            return Page();
        }

        if (!BookingDetailUi.AllowsDeposit(Booking.Status))
        {
            ErrorMessage = "Không thể thanh toán tiền cọc cho đơn này.";
            ViewState = "failed";
            CanSubmit = false;
            return Page();
        }

        if (Booking.QuotedDepositAmount is null)
        {
            ErrorMessage = "Đơn hàng chưa có thông tin tiền cọc.";
            ViewState = "failed";
            CanSubmit = false;
            return Page();
        }

        if (!PaymentUi.HasConcreteVehicle(Booking))
        {
            ErrorMessage = PaymentUi.MissingVehicle;
            ViewState = "failed";
            CanSubmit = false;
            return Page();
        }

        var payment = string.Equals(Deposit?.Status, "Pending", StringComparison.OrdinalIgnoreCase)
            ? Deposit
            : null;

        if (payment is null)
        {
            var (created, error) = await api.CreateDepositAsync(new CreatePaymentRequest(
                id,
                "Deposit",
                Method,
                null,
                Booking.AssignedVehicle?.VehicleId));
            if (created is null)
            {
                ErrorMessage = PaymentUi.PresentError(error);
                ViewState = "failed";
                await RefreshPaymentsAsync(id);
                return Page();
            }

            payment = created;
            Deposit = created;
        }

        var (paid, payError) = await api.SimulatePaymentSuccessAsync(payment.PaymentId);
        if (paid is null)
        {
            ErrorMessage = PaymentUi.PresentError(payError);
            ViewState = "failed";
            await RefreshPaymentsAsync(id);
            return Page();
        }

        Deposit = paid;
        Booking = await api.GetBookingAsync(id) ?? Booking;
        Presentation = PaymentUi.FromBooking(Booking, Deposit);
        ViewState = "success";
        CanSubmit = false;
        return Page();
    }

    private async Task LoadAsync(int id)
    {
        BookingRouteId = id;
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
                ErrorMessage = PaymentUi.AccessDenied;
                return;
            }

            if (data is null)
            {
                NotFoundBooking = true;
                ErrorMessage = status >= 500 ? PaymentUi.LoadFailure : PaymentUi.NotFound;
                return;
            }

            Booking = data;
            Payments = await api.GetBookingPaymentsAsync(id);
            Deposit = PaymentUi.ActiveDeposit(Payments);
            Presentation = PaymentUi.FromBooking(data, Deposit);
            ApplyViewState();
        }
        catch
        {
            ErrorMessage = PaymentUi.LoadFailure;
        }
    }

    private async Task RefreshPaymentsAsync(int id)
    {
        Payments = await api.GetBookingPaymentsAsync(id);
        Deposit = PaymentUi.ActiveDeposit(Payments);
        if (Booking is not null)
            Presentation = PaymentUi.FromBooking(Booking, Deposit);
        CanSubmit = Booking is not null
                    && BookingDetailUi.AllowsDeposit(Booking.Status)
                    && Booking.QuotedDepositAmount is not null
                    && PaymentUi.HasConcreteVehicle(Booking)
                    && !string.Equals(Deposit?.Status, "Paid", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyViewState()
    {
        if (string.Equals(Deposit?.Status, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            ViewState = "success";
            CanSubmit = false;
            return;
        }

        if (BookingDetailUi.IsAwaitingDispatcher(Booking?.Status))
        {
            CanSubmit = false;
            ViewState = "waiting";
            return;
        }

        if (Booking?.Status == "Cancelled")
        {
            CanSubmit = false;
            ViewState = "form";
            ErrorMessage ??= "Không thể thanh toán cho đơn đã hủy.";
            return;
        }

        if (Booking?.QuotedDepositAmount is null)
        {
            CanSubmit = false;
            ViewState = "form";
            ErrorMessage ??= "Đơn hàng chưa có thông tin tiền cọc.";
            return;
        }

        if (!PaymentUi.HasConcreteVehicle(Booking))
        {
            CanSubmit = false;
            ViewState = "form";
            ErrorMessage ??= PaymentUi.MissingVehicle;
            return;
        }

        if (string.Equals(Deposit?.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            ViewState = "pending";
            CanSubmit = true;
            return;
        }

        ViewState = "form";
        CanSubmit = true;
    }
}
