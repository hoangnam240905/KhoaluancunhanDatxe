using PortalWeb.Display;
using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Customer.Bookings;

public class DetailsModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public BookingResponse? Booking { get; set; }
    public List<PaymentResponse> Payments { get; set; } = [];
    public ContractResponse? Contract { get; set; }
    public IReadOnlyList<VehicleInspectionResponse> Inspections { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public string? InfoMessage { get; set; }
    public bool IsLoggedIn => auth.IsLoggedIn;
    public string? UserName => auth.FullName;
    public BookingDetailView? Presentation { get; private set; }
    public bool AccessDenied { get; private set; }
    public bool NotFoundBooking { get; private set; }
    public bool NeedsLogin { get; private set; }

    [BindProperty] public string Method { get; set; } = "BankTransfer";
    [BindProperty] public string? TransactionRef { get; set; }

    public static readonly string[] PaymentMethods = ["Cash", "BankTransfer", "MoMo", "VNPay"];

    public static bool IsSelfDrive(string? mode) => mode == "SelfDrive";
    public static string RentalModeLabel(string? mode) => mode == "SelfDrive" ? "Tự lái" : "Có tài xế";

    public static string FeeTypeLabel(string type) => type switch
    {
        "LateFee" => "Phí trễ hạn",
        "ExtraKm" => "Km vượt",
        "Fuel" => "Nhiên liệu",
        "Damage" => "Hư hỏng",
        "Other" => "Khác",
        _ => type
    };

    public static string InspectionTypeLabel(string type) => type switch
    {
        "Handover" => "Tình trạng xe khi nhận",
        "Return" => "Tình trạng xe khi trả",
        _ => type
    };

    public static decimal? ActualKm(IReadOnlyList<VehicleInspectionResponse> inspections)
    {
        var handover = inspections.LastOrDefault(i => i.InspectionType == "Handover")?.OdometerKm;
        var ret = inspections.LastOrDefault(i => i.InspectionType == "Return")?.OdometerKm;
        if (handover is null || ret is null) return null;
        return Math.Max(0, ret.Value - handover.Value);
    }

    public static string PaymentTypeLabel(string? type) => type switch
    {
        "Deposit" => "Tiền cọc",
        "Balance" => "Phần còn lại",
        "Refund" => "Hoàn tiền",
        _ => "Thanh toán cũ"
    };

    public static string PaymentStatusLabel(string status) => status switch
    {
        "Pending" => "Chờ thanh toán",
        "Paid" => "Đã thanh toán",
        "Failed" => "Thất bại",
        "Refunded" => "Đã hoàn tiền",
        _ => status
    };

    public static string PaymentMethodLabel(string method) => method switch
    {
        "Cash" => "Tiền mặt",
        "BankTransfer" => "Chuyển khoản",
        "MoMo" => "MoMo",
        "VNPay" => "VNPay",
        _ => method
    };

    public static string ContractStatusLabel(string status) => status switch
    {
        "Issued" => "Đã lập",
        "Signed" => "Đã ký (mô phỏng)",
        "Voided" => "Đã hủy hiệu lực",
        _ => status
    };

    public bool HasBlockingDeposit =>
        Payments.Any(p => p.PaymentType == "Deposit" && (p.Status is "Pending" or "Paid"));

    public bool IsAwaitingDispatcher => BookingDetailUi.IsAwaitingDispatcher(Booking?.Status);

    public bool CanViewContract =>
        Booking is not null &&
        BookingDetailUi.AllowsContract(Booking.Status) &&
        Booking.Status != "Cancelled";

    public bool CanPayDeposit =>
        Booking is not null &&
        Booking.QuotedDepositAmount is not null &&
        BookingDetailUi.AllowsDeposit(Booking.Status) &&
        !HasBlockingDeposit;

    public bool CanIssueContract =>
        Booking is not null &&
        Contract is null &&
        BookingDetailUi.AllowsDeposit(Booking.Status);

    public bool CanSignContract => false;

    public bool CanSimulate(PaymentResponse p) =>
        p.Status == "Pending" && Booking?.Status != "Cancelled";

    public bool HasPriceSnapshot =>
        Booking is not null &&
        (Booking.QuotedPricePerDay is not null ||
         Booking.QuotedPricePerKm is not null ||
         Booking.QuotedDays is not null ||
         Booking.QuotedDriverFeePerDay is not null ||
         Booking.QuotedSelfDriveIncludedKmPerDay is not null ||
         Booking.QuotedSelfDriveExtraKmPrice is not null ||
         Booking.QuotedDepositAmount is not null);

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;
        await LoadDetailAsync(id);
        if (NeedsLogin) return RedirectToPage("/Account/Login");
        return Page();
    }

    public async Task<IActionResult> OnPostDepositAsync(int id)
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;
        if (!await LoadAsync(id)) return RedirectToPage("Index");

        if (!CanPayDeposit)
        {
            ErrorMessage = IsAwaitingDispatcher
                ? BookingDetailUi.WaitingBody
                : "Không thể tạo khoản cọc cho đơn này.";
            return Page();
        }

        if (!PaymentMethods.Contains(Method))
            Method = "BankTransfer";

        var (payment, error) = await api.CreateDepositAsync(new CreatePaymentRequest(
            id, "Deposit", Method, string.IsNullOrWhiteSpace(TransactionRef) ? null : TransactionRef.Trim()));

        if (payment is null)
        {
            ErrorMessage = error;
            await LoadAsync(id);
            return Page();
        }

        InfoMessage = "Đã tạo yêu cầu thanh toán cọc. Trạng thái đang chờ thanh toán.";
        await LoadAsync(id);
        return Page();
    }

    public IActionResult OnPostIssueContract(int id)
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;
        return RedirectToPage("Contract", new { id });
    }

    public IActionResult OnPostSignContract(int id)
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;
        return RedirectToPage("Contract", new { id });
    }

    public async Task<IActionResult> OnPostSimulateSuccessAsync(int id, int paymentId)
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;
        if (!await LoadAsync(id)) return RedirectToPage("Index");
        var (payment, error) = await api.SimulatePaymentSuccessAsync(paymentId);
        if (payment is null)
        {
            ErrorMessage = error;
            await LoadAsync(id);
            return Page();
        }
        InfoMessage = "Mô phỏng thanh toán thành công. Cọc đã được ghi nhận Paid.";
        await LoadAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostSimulateFailureAsync(int id, int paymentId)
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;
        if (!await LoadAsync(id)) return RedirectToPage("Index");
        var (payment, error) = await api.SimulatePaymentFailureAsync(paymentId);
        if (payment is null)
        {
            ErrorMessage = error;
            await LoadAsync(id);
            return Page();
        }
        InfoMessage = "Mô phỏng thanh toán thất bại. Cọc không chuyển sang Paid.";
        await LoadAsync(id);
        return Page();
    }

    private async Task LoadDetailAsync(int id)
    {
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
                ErrorMessage = BookingDetailUi.AccessDenied;
                return;
            }

            if (data is null)
            {
                NotFoundBooking = true;
                ErrorMessage = status >= 500 ? BookingDetailUi.LoadFailure : BookingDetailUi.NotFound;
                return;
            }

            Booking = data;
            Presentation = BookingDetailUi.FromBooking(data);
            Payments = await api.GetBookingPaymentsAsync(id);
            Contract = await api.GetContractAsync(id);
        }
        catch
        {
            ErrorMessage = BookingDetailUi.LoadFailure;
        }
    }

    private async Task<bool> LoadAsync(int id)
    {
        Booking = await api.GetBookingAsync(id);
        if (Booking is null) return false;
        Payments = await api.GetBookingPaymentsAsync(id);
        Contract = await api.GetContractAsync(id);
        Inspections = Booking.Inspections is { Count: > 0 }
            ? Booking.Inspections
            : await api.GetBookingInspectionsAsync(id);
        Presentation = BookingDetailUi.FromBooking(Booking);
        return true;
    }
}
