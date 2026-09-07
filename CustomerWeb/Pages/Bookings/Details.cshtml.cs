using CustomerWeb.Models;
using CustomerWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CustomerWeb.Pages.Bookings;

public class DetailsModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public BookingResponse? Booking { get; set; }
    public List<PaymentResponse> Payments { get; set; } = [];
    public ContractResponse? Contract { get; set; }
    public IReadOnlyList<VehicleInspectionResponse> Inspections { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public string? InfoMessage { get; set; }

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
        "Handover" => "Giao xe",
        "Return" => "Trả xe",
        _ => type
    };

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

    public bool CanPayDeposit =>
        Booking is not null &&
        Booking.QuotedDepositAmount is not null &&
        Booking.Status != "Cancelled" &&
        !HasBlockingDeposit;

    public bool CanIssueContract =>
        Booking is not null &&
        Contract is null &&
        Booking.Status != "Cancelled";

    public bool CanSignContract =>
        Contract is not null &&
        Contract.Status == "Issued" &&
        Booking?.Status != "Cancelled";

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
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        if (!await LoadAsync(id)) return RedirectToPage("Index");
        return Page();
    }

    public async Task<IActionResult> OnPostDepositAsync(int id)
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        if (!await LoadAsync(id)) return RedirectToPage("Index");

        if (!CanPayDeposit)
        {
            ErrorMessage = "Không thể tạo khoản cọc cho đơn này.";
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

    public async Task<IActionResult> OnPostIssueContractAsync(int id)
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        if (!await LoadAsync(id)) return RedirectToPage("Index");
        var (contract, error) = await api.CreateContractAsync(id);
        if (contract is null)
        {
            ErrorMessage = error;
            await LoadAsync(id);
            return Page();
        }
        InfoMessage = "Đã lập hợp đồng điện tử.";
        await LoadAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostSignContractAsync(int id)
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        if (!await LoadAsync(id)) return RedirectToPage("Index");
        if (Contract is null)
        {
            ErrorMessage = "Chưa có hợp đồng.";
            return Page();
        }
        var (signed, error) = await api.SimulateSignContractAsync(Contract.ContractId);
        if (signed is null)
        {
            ErrorMessage = error;
            await LoadAsync(id);
            return Page();
        }
        InfoMessage = "Đã mô phỏng ký hợp đồng.";
        await LoadAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostSimulateSuccessAsync(int id, int paymentId)
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
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
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
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

    private async Task<bool> LoadAsync(int id)
    {
        Booking = await api.GetBookingAsync(id);
        if (Booking is null) return false;
        Payments = await api.GetBookingPaymentsAsync(id);
        Contract = await api.GetContractAsync(id);
        Inspections = Booking.Inspections is { Count: > 0 }
            ? Booking.Inspections
            : await api.GetBookingInspectionsAsync(id);
        return true;
    }
}
