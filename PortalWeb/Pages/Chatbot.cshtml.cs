using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PortalWeb.Display;
using PortalWeb.Models;
using PortalWeb.Services;

namespace PortalWeb.Pages;

/// <summary>
/// JSON handlers for the chat widget (_ChatWidget). Answers are public data, so Ask needs no antiforgery
/// token; Clear deletes the customer's own history and requires the widget's X-Requested-With header,
/// which a cross-site form cannot send. Builds the "Đặt xe" links the booking form already understands.
/// </summary>
[IgnoreAntiforgeryToken]
public class ChatbotModel(CarRentalApiClient api, AuthSession auth) : PageModel
{
    private bool IsCustomer => auth.IsLoggedIn && auth.Role == "Customer";

    public IActionResult OnGet() => Redirect("/");

    public async Task<IActionResult> OnPostAskAsync([FromForm] string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return new JsonResult(new { ok = false, error = "Bạn nhập câu hỏi nhé." }) { StatusCode = 400 };

        var (data, error) = await api.AskChatbotAsync(message.Trim());
        if (data is null)
            return new JsonResult(new { ok = false, error = error ?? "Chatbot tạm thời không phản hồi." }) { StatusCode = 502 };

        return new JsonResult(new { ok = true, answer = Shape(data) });
    }

    /// <summary>Saved chat of the logged-in customer (guests keep theirs in the browser tab).</summary>
    public async Task<IActionResult> OnGetHistoryAsync()
    {
        if (!IsCustomer)
            return new JsonResult(new { ok = true, messages = Array.Empty<object>() });

        var items = await api.GetChatbotHistoryAsync();
        var messages = items.Select(i => i.FromCustomer || i.Answer is null
            ? (object)new { me = i.FromCustomer, text = i.Text, at = i.CreatedAt }
            : new { me = false, text = i.Text, at = i.CreatedAt, answer = Shape(i.Answer) });
        return new JsonResult(new { ok = true, messages });
    }

    public async Task<IActionResult> OnPostClearAsync()
    {
        if (Request.Headers.XRequestedWith != "XMLHttpRequest")
            return BadRequest();
        if (!IsCustomer)
            return new JsonResult(new { ok = true });
        return new JsonResult(new { ok = await api.ClearChatbotHistoryAsync() });
    }

    private object Shape(ChatbotAskResponse data)
    {
        // A saved answer about dates already past gets no "Đặt xe" button.
        var canBook = data.Search is { } s && s.Start > UiDisplay.ToVietnam(DateTime.UtcNow);
        return new
        {
            data.Kind,
            data.Reply,
            vehicles = data.Vehicles.Select(v => new
            {
                v.VehicleId,
                v.Name,
                v.TypeName,
                v.Seats,
                v.LicensePlate,
                v.Days,
                v.TotalAmount,
                totalText = v.TotalAmount.ToString("#,0", CultureInfo.GetCultureInfo("vi-VN")) + " đ",
                bookUrl = canBook ? BookUrl(v.VehicleId, data.Search!) : null
            }),
            data.Links,
            loggedInCustomer = IsCustomer
        };
    }

    private static string BookUrl(int vehicleId, ChatbotSearchEcho s)
        => $"/Customer/Bookings/Create?vehicleId={vehicleId}"
           + $"&start={Uri.EscapeDataString(s.Start.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture))}"
           + $"&end={Uri.EscapeDataString(s.End.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture))}"
           + $"&rentalMode={Uri.EscapeDataString(s.RentalMode)}";
}