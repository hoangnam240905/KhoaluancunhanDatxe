using Xunit;

namespace Backend.Tests;

public class PhaseDispatcherBookingHubTests
{
    [Fact]
    public void DispatcherWeb_hub_uses_real_booking_apis_and_honest_hold_labels()
    {
        var view = Read("DispatcherWeb", "Pages", "Index.cshtml");
        var code = Read("DispatcherWeb", "Pages", "Index.cshtml.cs");
        var client = Read("DispatcherWeb", "Services", "CarRentalApiClient.cs");
        var hub = Read("DispatcherWeb", "Display", "BookingHubUi.cs");

        Assert.Contains("GetBookingsAsync()", code);
        Assert.Contains("GetBookingAsync", code);
        Assert.Contains("ConfirmBookingAsync", code);
        Assert.Contains("CancelBookingAsync", client);
        Assert.Contains("/api/bookings/{id}/status", client);
        Assert.Contains("GetAssignableAsync", code);
        Assert.DoesNotContain("AssignTripAsync", code);
        Assert.DoesNotContain("CreateAsync", code);
        Assert.Contains("Đơn thuê đã được xác nhận.", code);
        Assert.DoesNotContain("Đã giữ xe", code);
        Assert.DoesNotContain("Đã giữ xe", view);

        Assert.Contains("Xe khách yêu cầu", hub);
        Assert.Contains("Xe được phân công", hub);
        Assert.Contains("Chưa giữ", hub);
        Assert.Contains("Đã giữ", hub);
        Assert.Contains("HasActiveDeposit", hub);

        Assert.Contains("Chờ xác nhận", view);
        Assert.Contains("Xem chi tiết", view);
        Assert.Contains("Xác nhận", view);
        Assert.Contains("Không duyệt / Hủy đơn", view);
        Assert.Contains("Bạn có chắc muốn từ chối đơn thuê này?", view);
        Assert.Contains("asp-page-handler=\"Confirm\"", view);
        Assert.Contains("asp-page-handler=\"Reject\"", view);
        Assert.Contains("asp-page=\"/Handover\"", view);
        Assert.Contains(">Giao xe</a>", view);
        Assert.Contains("Tình trạng xe", view);
        Assert.Contains("SelfDrive không gắn tài xế", view);
        Assert.Contains("Khả năng đáp ứng", view);
        Assert.Contains("Không có tài xế khả dụng trong khoảng thời gian này.", view);
        Assert.Contains("title=\"Lịch điều phối là task riêng — chưa triển khai\"", view);
        Assert.DoesNotContain("fake", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DispatcherWeb_confirm_parses_backend_conflict_payload()
    {
        var client = Read("DispatcherWeb", "Services", "CarRentalApiClient.cs");
        Assert.Contains("AssignConflictResponse", client);
        Assert.Contains("conflict.ConflictType", client);
        Assert.Contains("VehicleAlternatives", Read("DispatcherWeb", "Pages", "Index.cshtml"));
        Assert.Contains("Xe thay thế khả dụng", Read("DispatcherWeb", "Pages", "Index.cshtml"));
    }

    private static string Read(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        string? root = null;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "Backend", "Backend.csproj")))
            {
                root = dir;
                break;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        Assert.False(string.IsNullOrEmpty(root));
        return File.ReadAllText(Path.Combine(new[] { root! }.Concat(parts).ToArray()));
    }
}
