using Microsoft.AspNetCore.Mvc;
using PortalWeb.Models;
using PortalWeb.Services;

namespace PortalWeb.Pages.Customer.Bookings;

/// <summary>Print-friendly view of one receipt / invoice (same HTML the customer receives by email).</summary>
public class InvoiceModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    public int BookingId { get; private set; }
    public InvoiceDocumentResponse? Document { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, string? number)
    {
        var denied = RequireRole(auth, "Customer");
        if (denied is not null) return denied;

        BookingId = id;
        // The Backend checks ownership: another customer's booking returns nothing.
        var docs = await api.GetBookingInvoicesAsync(id);
        Document = docs.FirstOrDefault(d => d.Number == number) ?? docs.FirstOrDefault();
        return Page();
    }
}
