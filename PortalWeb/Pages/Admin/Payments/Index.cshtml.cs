using PortalWeb.Models;
using PortalWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace PortalWeb.Pages.Admin.Payments;

public class IndexModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public List<PaymentResponse> Payments { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public int? FilterBookingId { get; set; }
    public string? FilterStatus { get; set; }
    public string? FilterPaymentType { get; set; }

    public async Task<IActionResult> OnGetAsync(int? bookingId, string? status, string? paymentType)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;
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
