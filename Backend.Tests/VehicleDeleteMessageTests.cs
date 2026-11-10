using Backend.Constants;
using Backend.Entities;
using Backend.Services;
using Backend.Validation;
using Xunit;

namespace Backend.Tests;

/// <summary>Deleting a car that has maintenance history explains why instead of a generic error.</summary>
public class VehicleDeleteMessageTests
{
    [Fact]
    public async Task Car_with_maintenance_history_cannot_be_deleted_and_the_reason_is_clear()
    {
        using var iso = new IsolatedCarRentalDb();
        var car = new Vehicle { TypeId = 1, LicensePlate = "51K-77701", Brand = "Test", Model = "Xoa", Year = 2024, Status = VehicleStatuses.Available };
        iso.Db.Vehicles.Add(car);
        iso.Db.SaveChanges();
        iso.Db.MaintenanceRecords.Add(new MaintenanceRecord
        {
            VehicleId = car.VehicleId, MaintenanceType = MaintenanceTypes.Scheduled,
            ScheduledDate = DateTime.UtcNow, CompletedDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow
        });
        iso.Db.SaveChanges();

        var (ok, error, status) = await new VehicleService(iso.Db).DeleteVehicleAsync(car.VehicleId);

        Assert.False(ok);
        Assert.Equal(400, status);
        Assert.Equal(VehicleOccupancyRules.CannotDeleteWithMaintenance, error);
        Assert.NotNull(iso.Db.Vehicles.Find(car.VehicleId));
    }
}