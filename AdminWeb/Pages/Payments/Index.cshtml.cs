using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminWeb.Pages.Payments;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public List<PaymentResponse> Payments { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public int? FilterBookingId { get; set; }
    public string? FilterStatus { get; set; }
    public string? FilterPaymentType { get; set; }

    public async Task<IActionResult> OnGetAsync(int? bookingId, string? status, string? paymentType)
    {
        if (!auth.IsLoggedIn) return Redirect("http://localhost:5180/Account/Login");
        FilterBookingId = bookingId;
        FilterStatus = status;
        FilterPaymentType = paymentType;
        var (data, error) = await api.GetAdminPaymentsAsync(bookingId, status, paymentType);
        if (data is null)
        {
            ErrorMessage = error;
            Payments = [];
            return Page();
        }
        Payments = data;
        return Page();
    }

    public static string PaymentTypeLabel(string? type) =>
        string.IsNullOrWhiteSpace(type) ? "Thanh toán cũ" : type;
}
