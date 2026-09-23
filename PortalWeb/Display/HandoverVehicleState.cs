using PortalWeb.Models;
using PortalWeb.Services;

namespace PortalWeb.Display;

/// <summary>
/// Shared Dispatcher SelfDrive handover resolver — applies to EVERY vehicle/booking dynamically.
/// Never hardcodes VehicleId / plate / booking. Never invents Fuel. CurrentKm=0 and Fuel=0 are valid.
/// </summary>
public static class HandoverVehicleState
{
    public const string KmMissingMessage =
        "Xe chưa có số KM hiện tại trong hệ thống.";

    public const string FuelRequiredMessage =
        "Vui lòng nhập mức nhiên liệu từ 0 đến 100.";

    public const string FuelInvalidMessage =
        "Mức nhiên liệu phải từ 0 đến 100.";

    public sealed record Snapshot(
        int VehicleId,
        string VehicleName,
        string LicensePlate,
        /// <summary>Non-null when vehicle record was loaded. Value may be 0 (valid).</summary>
        int? CurrentKm,
        decimal? FuelLevel,
        bool RequiresFuelInput,
        string? BlockReason);

    public static async Task<Snapshot?> ResolveAsync(
        CarRentalApiClient api,
        BookingResponse booking,
        IReadOnlyList<VehicleInspectionResponse>? inspectionsAlreadyLoaded = null)
    {
        // Always resolve from THIS booking's assigned vehicle — never another vehicle.
        var vehicleId = booking.AssignedVehicle?.VehicleId ?? booking.Assignment?.VehicleId;
        if (vehicleId is null or <= 0)
            return new Snapshot(0, "—", "—", null, null, false, "Đơn chưa được gán xe.");

        var vehicle = await api.GetVehicleAsync(vehicleId.Value);
        if (vehicle is null)
            return new Snapshot(vehicleId.Value, "—", "—", null, null, false, KmMissingMessage);

        var name = $"{vehicle.Brand} {vehicle.Model}".Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = booking.VehicleTypeName;

        // Fuel for THIS vehicleId only.
        var fuel = FindLatestFuel(inspectionsAlreadyLoaded, vehicle.VehicleId);

        if (fuel is null && inspectionsAlreadyLoaded is null)
        {
            var (inspections, inspError) = await api.GetDispatchInspectionsAsync();
            if (inspError is null)
                fuel = FindLatestFuel(inspections, vehicle.VehicleId);
        }

        if (fuel is null)
        {
            var (profile, _) = await api.GetVehicleOperationalProfileAsync(vehicle.VehicleId);
            // 0% and 100% are valid known levels; only null means "not present".
            if (profile is not null && profile.LatestFuelLevel is decimal profileFuel
                && IsValidFuelPercent(profileFuel))
                fuel = profileFuel;
        }

        // Vehicle.CurrentKm is int in API — always present when vehicle loads.
        // Treat negative as invalid; 0 is valid.
        int? currentKm = vehicle.CurrentKm;
        string? block = null;
        if (currentKm < 0)
        {
            currentKm = null;
            block = KmMissingMessage;
        }

        return new Snapshot(
            vehicle.VehicleId,
            name,
            vehicle.LicensePlate,
            currentKm,
            fuel,
            RequiresFuelInput: fuel is null,
            block);
    }

    /// <summary>
    /// CurrentKm always from snapshot (backend). Fuel: known backend value (incl. 0), else client input.
    /// </summary>
    public static (decimal OdometerKm, decimal FuelLevel, string? Error) ResolveHandoverCondition(
        Snapshot snap,
        decimal? clientFuelLevel)
    {
        if (snap.BlockReason is not null)
            return (0, 0, snap.BlockReason);

        // Distinguish null (missing) from 0 (valid).
        if (snap.CurrentKm is null)
            return (0, 0, KmMissingMessage);
        if (snap.CurrentKm < 0)
            return (0, 0, KmMissingMessage);

        var odometer = (decimal)snap.CurrentKm.Value;

        if (!snap.RequiresFuelInput)
        {
            if (snap.FuelLevel is decimal known && IsValidFuelPercent(known))
                return (odometer, known, null);
            // Inconsistent snapshot — fall through to client input requirement.
        }

        if (clientFuelLevel is null)
            return (0, 0, FuelRequiredMessage);
        if (!IsValidFuelPercent(clientFuelLevel.Value))
            return (0, 0, FuelInvalidMessage);

        return (odometer, clientFuelLevel.Value, null);
    }

    public static bool IsValidFuelPercent(decimal fuel)
        => fuel is >= 0 and <= 100;

    public static decimal? FindLatestFuel(
        IReadOnlyList<VehicleInspectionResponse>? inspections,
        int vehicleId)
    {
        if (inspections is null || inspections.Count == 0)
            return null;

        // Strict vehicle scope — never mix fuel from another vehicle.
        var forVehicle = inspections
            .Where(i => i.VehicleId == vehicleId && i.FuelLevel is not null && IsValidFuelPercent(i.FuelLevel.Value))
            .OrderByDescending(i => i.InspectionId)
            .ToList();

        if (forVehicle.Count == 0)
            return null;

        var latestReturn = forVehicle.FirstOrDefault(i =>
            string.Equals(i.InspectionType, "Return", StringComparison.OrdinalIgnoreCase));
        return latestReturn?.FuelLevel ?? forVehicle[0].FuelLevel;
    }
}
