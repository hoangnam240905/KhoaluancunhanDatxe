using PortalWeb.Models;

namespace PortalWeb.Display;

/// <summary>
/// Quick "Phân công xe & tài xế" on the Hub: the car and the driver the customer chose when booking come first
/// (when they are still in the assignable list — free in the time slot incl. the 2-hour buffer, qualified);
/// otherwise the first assignable one, with a note saying why the customer's choice was not used.
/// Manual choice of any car / driver is on "Lịch xe & tài xế".
/// </summary>
public sealed record AssignPick(int VehicleId, int? DriverId, string? Note)
{
    public static (AssignPick? Pick, string? Error) For(BookingResponse booking, DispatchAssignableResponse assignable)
    {
        var notes = new List<string>();

        var wantedVehicleId = booking.AssignedVehicle?.VehicleId;
        var vehicle = (wantedVehicleId is int wv ? assignable.Vehicles.FirstOrDefault(v => v.VehicleId == wv) : null)
            ?? assignable.Vehicles.FirstOrDefault();
        if (vehicle is null)
            return (null, "Không có xe khả dụng để phân công.");
        if (wantedVehicleId is int wanted && vehicle.VehicleId != wanted)
            notes.Add($"Xe khách chọn ({booking.AssignedVehicle!.LicensePlate}) không còn khả dụng trong khung giờ này nên đã phân công xe {vehicle.LicensePlate}.");

        int? driverId = null;
        if (string.Equals(booking.RentalMode, "WithDriver", StringComparison.OrdinalIgnoreCase))
        {
            var requested = booking.RequestedDriverId is int rd ? assignable.Drivers.FirstOrDefault(d => d.DriverId == rd) : null;
            var driver = requested ?? assignable.Drivers.FirstOrDefault();
            if (driver is null)
                return (null, booking.RequestedDriverId is null
                    ? "Không có tài xế khả dụng để phân công."
                    : $"Tài xế khách chọn ({RequestedName(booking)}) và các tài xế khác đều không khả dụng trong khung giờ này.");
            if (booking.RequestedDriverId is not null && requested is null)
                notes.Add($"Tài xế khách chọn ({RequestedName(booking)}) đang bận / trùng lịch (tính cả 2 giờ đệm) hoặc chưa đủ điều kiện (chưa xác minh, bằng lái không đủ hạng hay hết hạn) nên đã phân công tài xế {driver.FullName}.");
            driverId = driver.DriverId;
        }

        return (new AssignPick(vehicle.VehicleId, driverId, notes.Count == 0 ? null : string.Join(" ", notes)), null);
    }

    private static string RequestedName(BookingResponse booking)
        => string.IsNullOrWhiteSpace(booking.RequestedDriverName) ? $"#{booking.RequestedDriverId}" : booking.RequestedDriverName!;
}
