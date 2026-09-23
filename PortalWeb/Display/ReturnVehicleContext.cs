using PortalWeb.Models;
using PortalWeb.Services;

namespace PortalWeb.Display;

/// <summary>
/// Display context for Dispatcher SelfDrive return. Return KM/Fuel are entered by the dispatcher —
/// never copied from CurrentKm and never hardcoded.
/// </summary>
public static class ReturnVehicleContext
{
    public const string KmRequiredMessage = "Vui lòng nhập số km khi trả xe.";
    public const string KmInvalidMessage = "Số km khi trả xe không hợp lệ.";
    public const string KmNegativeMessage = "Số km khi trả xe không được âm.";
    public const string FuelRequiredMessage = "Vui lòng nhập mức nhiên liệu khi trả xe từ 0 đến 100.";
    public const string FuelInvalidMessage = "Mức nhiên liệu phải từ 0 đến 100.";

    public sealed record Info(
        int VehicleId,
        string VehicleName,
        string LicensePlate,
        decimal? HandoverOdometerKm);

    public static async Task<(Info? Data, string? Error)> LoadAsync(CarRentalApiClient api, BookingResponse booking)
    {
        if (!string.Equals(booking.RentalMode, "SelfDrive", StringComparison.OrdinalIgnoreCase))
            return (null, "Chỉ đơn tự lái mới tiếp nhận trả xe tại điều phối.");
        if (!string.Equals(booking.Status, "InProgress", StringComparison.OrdinalIgnoreCase))
            return (null, "Chỉ tiếp nhận trả xe khi đơn đang trong quá trình thuê.");

        var vehicleId = booking.AssignedVehicle?.VehicleId ?? booking.Assignment?.VehicleId;
        if (vehicleId is null or <= 0)
            return (null, "Đơn chưa được gán xe.");

        var vehicle = await api.GetVehicleAsync(vehicleId.Value);
        if (vehicle is null)
            return (null, "Không lấy được thông tin xe.");

        var name = $"{vehicle.Brand} {vehicle.Model}".Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = booking.VehicleTypeName;

        decimal? handoverKm = null;
        var (inspections, _) = await api.GetDispatchInspectionsAsync(booking.BookingId);
        var handover = inspections
            .Where(i => string.Equals(i.InspectionType, "Handover", StringComparison.OrdinalIgnoreCase)
                        && i.OdometerKm is not null)
            .OrderByDescending(i => i.InspectionId)
            .FirstOrDefault();
        if (handover?.OdometerKm is decimal km)
            handoverKm = km;

        return (new Info(vehicle.VehicleId, name, vehicle.LicensePlate, handoverKm), null);
    }

    /// <summary>Validates dispatcher-entered return odometer and fuel. Does not compare to handover KM (backend does).</summary>
    public static (decimal OdometerKm, decimal FuelLevel, string? Error) ValidateInput(decimal? odometerKm, decimal? fuelLevel)
    {
        if (odometerKm is null)
            return (0, 0, KmRequiredMessage);
        if (odometerKm < 0)
            return (0, 0, KmNegativeMessage);

        if (fuelLevel is null)
            return (0, 0, FuelRequiredMessage);
        if (fuelLevel is < 0 or > 100)
            return (0, 0, FuelInvalidMessage);

        return (odometerKm.Value, fuelLevel.Value, null);
    }
}
