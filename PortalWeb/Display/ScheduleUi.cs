using System.Globalization;
using PortalWeb.Models;

namespace PortalWeb.Display;

/// <summary>Maps Backend DTOs into Mockup Schedule client shapes.</summary>
public static class ScheduleUi
{
    public static string VehicleKey(int id) => "v" + id;
    public static string DriverKey(int id) => "d" + id;

    public static int? ParseVehicleKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        if (key.StartsWith('v') || key.StartsWith('V'))
            return int.TryParse(key.AsSpan(1), out var id) ? id : null;
        return int.TryParse(key, out var n) ? n : null;
    }

    public static int? ParseDriverKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        if (key.StartsWith('d') || key.StartsWith('D'))
            return int.TryParse(key.AsSpan(1), out var id) ? id : null;
        return int.TryParse(key, out var n) ? n : null;
    }

    public static object ToVehicle(VehicleResponse v) => new
    {
        id = VehicleKey(v.VehicleId),
        name = $"{v.Brand} {v.Model}".Trim(),
        plate = v.LicensePlate,
        status = MapVehicleStatus(v.Status)
    };

    public static object ToVehicleFromFleet(DispatchVehicleStatusResponse v) => new
    {
        id = VehicleKey(v.VehicleId),
        name = string.IsNullOrWhiteSpace(v.TypeName) ? v.LicensePlate : v.TypeName,
        plate = v.LicensePlate,
        status = MapVehicleStatus(v.VehicleStatus)
    };

    public static object ToDriver(DriverResponse d) => new
    {
        id = DriverKey(d.DriverId),
        name = d.FullName,
        status = MapDriverStatus(d.Status)
    };

    public static object ToDriverFromFleet(DispatchDriverStatusResponse d) => new
    {
        id = DriverKey(d.DriverId),
        name = d.FullName,
        status = MapDriverStatus(d.DriverStatus)
    };

    public static object? ToBooking(BookingResponse b)
    {
        var vehicleId = b.AssignedVehicle?.VehicleId ?? b.Assignment?.VehicleId;
        var driverId = b.Assignment?.DriverId;
        // Unassigned bookings have no concrete vehicle lane — skip gantt bars.
        if (vehicleId is null) return null;

        return new
        {
            id = b.BookingId,
            customer = b.CustomerName,
            mode = string.IsNullOrWhiteSpace(b.RentalMode) ? "WithDriver" : b.RentalMode,
            vehicleId = VehicleKey(vehicleId.Value),
            driverId = driverId is int did ? DriverKey(did) : null,
            start = b.StartDate.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            end = b.EndDate.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            status = b.Status,
            pickup = b.PickupAddress,
            dropoff = b.DropoffAddress
        };
    }

    public static object ToFleetRow(DispatchVehicleStatusResponse v) => new
    {
        vehicleId = VehicleKey(v.VehicleId),
        name = string.IsNullOrWhiteSpace(v.TypeName) ? v.LicensePlate : v.TypeName,
        plate = v.LicensePlate,
        status = MapVehicleStatus(v.VehicleStatus),
        bookingId = v.BookingId,
        start = v.StartDate?.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
        end = v.EndDate?.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
        driverName = v.DriverName,
        rentalMode = v.RentalMode
    };

    public static object ToAssignableVehicle(VehicleResponse v) => new
    {
        id = VehicleKey(v.VehicleId),
        name = $"{v.Brand} {v.Model}".Trim(),
        plate = v.LicensePlate,
        status = MapVehicleStatus(v.Status)
    };

    public static object ToAssignableDriver(DriverResponse d) => new
    {
        id = DriverKey(d.DriverId),
        name = d.FullName,
        status = MapDriverStatus(d.Status)
    };

    public static object ToScheduleBooking(BookingResponse b) =>
        ToBooking(b) ?? new
        {
            id = b.BookingId,
            customer = b.CustomerName,
            mode = string.IsNullOrWhiteSpace(b.RentalMode) ? "WithDriver" : b.RentalMode,
            vehicleId = (string?)null,
            driverId = b.Assignment is { DriverId: int did } ? DriverKey(did) : null,
            start = b.StartDate.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            end = b.EndDate.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            status = b.Status,
            pickup = b.PickupAddress,
            dropoff = b.DropoffAddress
        };

    private static string MapVehicleStatus(string? status) => status?.Trim() switch
    {
        "Available" => "Available",
        "Busy" or "Rented" or "InUse" => "Busy",
        "Maintenance" or "Unavailable" => "Maintenance",
        "Preparing" or "Reserved" => "Preparing",
        _ => string.IsNullOrWhiteSpace(status) ? "Available" : status
    };

    private static string MapDriverStatus(string? status) => status?.Trim() switch
    {
        "Available" => "Available",
        "Busy" or "OnTrip" or "Assigned" => "Busy",
        "Offline" or "Inactive" => "Offline",
        _ => string.IsNullOrWhiteSpace(status) ? "Available" : status
    };
}
