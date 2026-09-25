using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PortalWeb.Models;
using PortalWeb.Services;

namespace PortalWeb.Pages.AdminUi;

public class DashboardModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    private static readonly JsonSerializerOptions ChartJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public DashboardResponse? Dashboard { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string WelcomeName { get; private set; } = "Admin";
    public string ChartPayloadJson { get; private set; } = "null";

    public async Task<IActionResult> OnGetAsync(DateTime? from = null, DateTime? to = null)
    {
        var denied = RequireRole(auth, "Admin");
        if (denied is not null) return denied;

        WelcomeName = FirstName(auth.FullName) ?? "Admin";

        DateTime? rangeFrom = from?.Date;
        DateTime? rangeTo = to?.Date;
        if (rangeFrom is not null && rangeTo is not null && rangeTo < rangeFrom)
            (rangeFrom, rangeTo) = (rangeTo, rangeFrom);
        if (rangeTo is not null)
            rangeTo = rangeTo.Value.Date.AddDays(1).AddTicks(-1);

        var (data, error) = await api.GetAdminDashboardAsync(rangeFrom, rangeTo);
        if (data is null)
        {
            ErrorMessage = "Không thể tải dữ liệu tổng quan. Vui lòng thử lại.";
            ChartPayloadJson = "null";
            return Page();
        }

        Dashboard = data;
        ChartPayloadJson = JsonSerializer.Serialize(BuildChartPayload(data), ChartJsonOptions);
        return Page();
    }

    private static object BuildChartPayload(DashboardResponse d)
    {
        var revenue = d.RevenueOverview ?? [];
        var topMax = d.TopVehicles.Count == 0 ? 1 : d.TopVehicles.Max(v => v.CompletedCount);
        return new
        {
            revenue = new
            {
                months = revenue.Select(r => r.Label).ToList(),
                values = revenue.Select(r => r.Amount).ToList()
            },
            bookingStatus = new[]
            {
                new { label = "Confirmed", value = d.Bookings.Confirmed + d.Bookings.Assigned, color = "#16A34A" },
                new { label = "Pending", value = d.Bookings.Pending, color = "#D97706" },
                new { label = "In Progress", value = d.Bookings.InProgress, color = "#2563EB" },
                new { label = "Cancelled", value = d.Bookings.Cancelled, color = "#DC2626" }
            },
            topVehicles = d.TopVehicles.Take(5).Select(v => new
            {
                name = $"{v.Brand} {v.Model}".Trim(),
                bookings = v.CompletedCount,
                pct = Math.Max(8, (int)Math.Round(v.CompletedCount * 100.0 / topMax))
            }).ToList()
        };
    }

    private static string? FirstName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return null;
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? null : parts[^1];
    }
}
