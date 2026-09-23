using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PortalWeb.Display;
using PortalWeb.Models;
using PortalWeb.Services;

namespace PortalWeb.Pages.Mockup;

public class HubModel(CarRentalApiClient api, AuthSession auth) : RolePageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string BookingsJson { get; private set; } = "[]";
    public string? LoadError { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;

        var (rows, error) = await LoadRowsAsync();
        LoadError = error;
        BookingsJson = JsonSerializer.Serialize(rows, JsonOpts);
        return Page();
    }

    public async Task<IActionResult> OnGetListAsync()
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;
        var (rows, error) = await LoadRowsAsync();
        if (error is not null) return new JsonResult(new { ok = false, error }) { StatusCode = 502 };
        return new JsonResult(new { ok = true, bookings = rows });
    }

    public async Task<IActionResult> OnGetDetailAsync(int id)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;

        var booking = await api.GetBookingAsync(id);
        if (booking is null) return new JsonResult(new { ok = false, error = "Không tìm thấy đơn." }) { StatusCode = 404 };

        var contract = await api.GetContractAsync(id);
        var row = HubBookingUi.ToHubRow(booking, contract?.Status);
        return new JsonResult(new { ok = true, booking = row });
    }

    public async Task<IActionResult> OnPostConfirmAsync(int id)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;

        var (data, error) = await api.ConfirmBookingAsync(id);
        if (data is null) return ActionFail(error ?? "Không xác nhận được đơn.");
        return await ActionOkAsync(data);
    }

    public async Task<IActionResult> OnPostRejectAsync(int id)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;

        var (data, error) = await api.UpdateBookingStatusAsync(id, "Cancelled", "Điều phối không duyệt / hủy đơn");
        if (data is null) return ActionFail(error ?? "Không hủy được đơn.");
        return await ActionOkAsync(data);
    }

    public async Task<IActionResult> OnPostAssignAsync(int id)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;

        var booking = await api.GetBookingAsync(id);
        if (booking is null) return ActionFail("Không tìm thấy đơn.");
        if (!string.Equals(booking.Status, "Confirmed", StringComparison.OrdinalIgnoreCase))
            return ActionFail("Chỉ phân công đơn đã xác nhận.");

        var (assignable, aErr) = await api.GetAssignableAsync(id);
        if (assignable is null) return ActionFail(aErr ?? "Không tải được danh sách xe/tài xế.");

        var vehicle = assignable.Vehicles.FirstOrDefault();
        if (vehicle is null) return ActionFail("Không có xe khả dụng để phân công.");

        int? driverId = null;
        if (string.Equals(booking.RentalMode, "WithDriver", StringComparison.OrdinalIgnoreCase))
        {
            var driver = assignable.Drivers.FirstOrDefault();
            if (driver is null) return ActionFail("Không có tài xế khả dụng để phân công.");
            driverId = driver.DriverId;
        }

        var (data, error, conflict) = await api.AssignTripAsync(id, new AssignTripRequest(driverId, vehicle.VehicleId));
        if (data is null)
        {
            var msg = conflict?.Message ?? error ?? "Phân công thất bại.";
            if (conflict?.VehicleAlternatives.Count > 0 || conflict?.DriverAlternatives.Count > 0)
                msg += " Có phương án thay thế — dùng trang Phân công chi tiết nếu cần chọn thủ công.";
            return ActionFail(msg);
        }

        return await ActionOkAsync(data);
    }

    public async Task<IActionResult> OnGetHandoverInfoAsync(int id)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;

        var booking = await api.GetBookingAsync(id);
        if (booking is null)
            return new JsonResult(new { ok = false, error = "Không tìm thấy đơn." }) { StatusCode = 404 };

        if (!string.Equals(booking.RentalMode, "SelfDrive", StringComparison.OrdinalIgnoreCase))
            return new JsonResult(new { ok = false, error = "Chỉ đơn tự lái mới dùng giao xe tại điều phối." });

        if (!string.Equals(booking.Status, "Assigned", StringComparison.OrdinalIgnoreCase))
            return new JsonResult(new { ok = false, error = "Chỉ giao xe khi đơn đã được gán xe." });

        var snap = await HandoverVehicleState.ResolveAsync(api, booking);
        if (snap is null)
            return new JsonResult(new { ok = false, error = "Không lấy được thông tin xe." }) { StatusCode = 404 };

        var kmOk = snap.BlockReason is null && snap.CurrentKm is not null;
        return new JsonResult(new
        {
            ok = true,
            bookingId = booking.BookingId,
            vehicleId = snap.VehicleId,
            vehicle = snap.VehicleName,
            plate = snap.LicensePlate,
            currentKm = snap.CurrentKm,
            fuelLevel = snap.FuelLevel,
            requiresFuelInput = snap.RequiresFuelInput,
            canConfirm = kmOk && !snap.RequiresFuelInput,
            blockReason = snap.BlockReason
        });
    }

    public async Task<IActionResult> OnPostHandoverAsync(int id, decimal? odometerKm, decimal? fuelLevel)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;

        var booking = await api.GetBookingAsync(id);
        if (booking is null) return ActionFail("Không tìm thấy đơn.");

        var snap = await HandoverVehicleState.ResolveAsync(api, booking);
        if (snap is null)
            return ActionFail("Không lấy được thông tin xe.");

        var (odo, fuel, err) = HandoverVehicleState.ResolveHandoverCondition(snap, fuelLevel);
        if (err is not null)
            return ActionFail(err);

        var condition = new VehicleConditionRequest
        {
            OdometerKm = odo,
            FuelLevel = fuel
        };

        var (data, error) = await api.HandoverBookingAsync(id, condition);
        if (data is null) return ActionFail(error ?? "Giao xe thất bại (API SelfDrive).");
        return await ActionOkAsync(data);
    }

    public async Task<IActionResult> OnGetReturnInfoAsync(int id)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;

        var booking = await api.GetBookingAsync(id);
        if (booking is null)
            return new JsonResult(new { ok = false, error = "Không tìm thấy đơn." }) { StatusCode = 404 };

        var (info, error) = await ReturnVehicleContext.LoadAsync(api, booking);
        if (info is null)
            return new JsonResult(new { ok = false, error = error ?? "Không thể tải dữ liệu trả xe." });

        return new JsonResult(new
        {
            ok = true,
            bookingId = booking.BookingId,
            vehicleId = info.VehicleId,
            vehicle = info.VehicleName,
            plate = info.LicensePlate,
            handoverOdometerKm = info.HandoverOdometerKm
        });
    }

    public async Task<IActionResult> OnPostCompleteAsync(int id, decimal? odometerKm, decimal? fuelLevel)
    {
        var denied = RequireRole(auth, "Dispatcher");
        if (denied is not null) return denied;

        var (odo, fuel, err) = ReturnVehicleContext.ValidateInput(odometerKm, fuelLevel);
        if (err is not null)
            return ActionFail(err);

        var (data, error) = await api.CompleteSelfDriveAsync(id, new VehicleConditionRequest
        {
            OdometerKm = odo,
            FuelLevel = fuel
        });
        if (data is null) return ActionFail(error ?? "Hoàn thành thất bại (API SelfDrive).");
        return await ActionOkAsync(data);
    }

    private async Task<(List<object> Rows, string? Error)> LoadRowsAsync()
    {
        try
        {
            var list = await api.GetBookingsAsync();
            var rows = list
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => HubBookingUi.ToHubRow(b))
                .Cast<object>()
                .ToList();
            return (rows, null);
        }
        catch (Exception ex)
        {
            return ([], "Không tải được danh sách đơn: " + ex.Message);
        }
    }

    private async Task<IActionResult> ActionOkAsync(BookingResponse data)
    {
        var contract = await api.GetContractAsync(data.BookingId);
        return new JsonResult(new { ok = true, booking = HubBookingUi.ToHubRow(data, contract?.Status) });
    }

    private static IActionResult ActionFail(string error) =>
        new JsonResult(new { ok = false, error });
}
