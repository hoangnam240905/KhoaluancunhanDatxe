using Backend.Constants;
using Backend.Entities;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

/// <summary>General fuel/km edge cases for handover — no vehicle-specific hardcoding.</summary>
public class PhaseHandoverFuelEdgeTests
{
    [Fact]
    public async Task Latest_fuel_zero_percent_is_valid_known_level_not_missing()
    {
        using var iso = new IsolatedCarRentalDb();
        var vehicle = await iso.Db.Vehicles.AsNoTracking().FirstAsync();
        var bookingId = await iso.Db.Bookings.AsNoTracking().Select(b => b.BookingId).FirstAsync();
        iso.Db.VehicleInspections.Add(new VehicleInspection
        {
            BookingId = bookingId,
            VehicleId = vehicle.VehicleId,
            InspectionType = VehicleInspectionTypes.Return,
            ActualAt = DateTime.UtcNow,
            OdometerKm = vehicle.CurrentKm,
            FuelLevel = 0m,
            CreatedAt = DateTime.UtcNow
        });
        await iso.Db.SaveChangesAsync();

        var fuel = await new VehicleInspectionService(iso.Db).GetLatestFuelLevelAsync(vehicle.VehicleId);
        Assert.Equal(0m, fuel);
    }

    [Fact]
    public async Task Latest_fuel_100_percent_is_valid()
    {
        using var iso = new IsolatedCarRentalDb();
        var vehicle = await iso.Db.Vehicles.AsNoTracking().FirstAsync();
        var bookingId = await iso.Db.Bookings.AsNoTracking().Select(b => b.BookingId).FirstAsync();
        iso.Db.VehicleInspections.Add(new VehicleInspection
        {
            BookingId = bookingId,
            VehicleId = vehicle.VehicleId,
            InspectionType = VehicleInspectionTypes.Return,
            ActualAt = DateTime.UtcNow,
            OdometerKm = vehicle.CurrentKm,
            FuelLevel = 100m,
            CreatedAt = DateTime.UtcNow
        });
        await iso.Db.SaveChangesAsync();

        var fuel = await new VehicleInspectionService(iso.Db).GetLatestFuelLevelAsync(vehicle.VehicleId);
        Assert.Equal(100m, fuel);
    }

    [Fact]
    public async Task Latest_fuel_is_scoped_to_requested_vehicle_only()
    {
        using var iso = new IsolatedCarRentalDb();
        var vehicles = await iso.Db.Vehicles.AsNoTracking().OrderBy(v => v.VehicleId).Take(2).ToListAsync();
        Assert.True(vehicles.Count >= 2);
        var a = vehicles[0];
        var b = vehicles[1];
        var bookingId = await iso.Db.Bookings.AsNoTracking().Select(x => x.BookingId).FirstAsync();

        iso.Db.VehicleInspections.Add(new VehicleInspection
        {
            BookingId = bookingId,
            VehicleId = a.VehicleId,
            InspectionType = VehicleInspectionTypes.Return,
            ActualAt = DateTime.UtcNow,
            OdometerKm = a.CurrentKm,
            FuelLevel = 55m,
            CreatedAt = DateTime.UtcNow
        });
        await iso.Db.SaveChangesAsync();

        var svc = new VehicleInspectionService(iso.Db);
        Assert.Equal(55m, await svc.GetLatestFuelLevelAsync(a.VehicleId));
        Assert.Null(await svc.GetLatestFuelLevelAsync(b.VehicleId));
    }
}
