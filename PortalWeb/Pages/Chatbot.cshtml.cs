using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PortalWeb.Services;

namespace PortalWeb.Pages;

/// <summary>
/// JSON handler for the chat widget (_ChatWidget). Read-only and public (no personal data), so it
/// does not need an antiforgery token. Builds the "Đặt xe" links the booking form already understands.
/// </summary>
[IgnoreAntiforgeryToken]
public class ChatbotModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    public IActionResult OnGet() => Redirect("/");

    public async Task<IActionResult> OnPostAskAsync([FromForm] string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return new JsonResult(new { ok = false, error = "Bạn nhập câu hỏi nhé." }) { StatusCode = 400 };

        var (data, error) = await api.AskChatbotAsync(message.Trim());
        if (data is null)
            return new JsonResult(new { ok = false, error = error ?? "Chatbot tạm thời không phản hồi." }) { StatusCode = 502 };

        var loggedInCustomer = auth.IsLoggedIn && auth.Role == "Customer";
        var vehicles = data.Vehicles.Select(v => new
        {
            v.VehicleId,
            v.Name,
            v.TypeName,
            v.Seats,
            v.LicensePlate,
            v.Days,
            v.TotalAmount,
            totalText = v.TotalAmount.ToString("#,0", CultureInfo.GetCultureInfo("vi-VN")) + " đ",
            bookUrl = data.Search is null ? null : BookUrl(v.VehicleId, data.Search)
        });
        return new JsonResult(new { ok = true, data.Kind, data.Reply, vehicles, data.Links, loggedInCustomer });
    }

    private static string BookUrl(int vehicleId, PortalWeb.Models.ChatbotSearchEcho s)
        => $"/Customer/Bookings/Create?vehicleId={vehicleId}"
           + $"&start={Uri.EscapeDataString(s.Start.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture))}"
           + $"&end={Uri.EscapeDataString(s.End.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture))}"
           + $"&rentalMode={Uri.EscapeDataString(s.RentalMode)}";
}
