using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class VehicleService(CarRentalDbContext db)
{
    public async Task<List<VehicleTypeResponse>> GetVehicleTypesAsync()
    {
        return await db.VehicleTypes
            .Where(vt => vt.IsActive)
            .OrderBy(vt => vt.SeatCapacity)
            .Select(vt => new VehicleTypeResponse(
                vt.TypeId, vt.TypeName, vt.SeatCapacity, vt.PricePerDay,
                vt.PricePerKm, vt.Description, vt.ImageUrl))
            .ToListAsync();
    }

    public async Task<List<VehicleResponse>> GetVehiclesAsync(string? status = null)
    {
        var query = db.Vehicles.Include(v => v.VehicleType).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(v => v.Status == status);

        return await query
            .OrderBy(v => v.Brand)
            .Select(v => new VehicleResponse(
                v.VehicleId, v.TypeId, v.VehicleType.TypeName, v.LicensePlate,
                v.Brand, v.Model, v.Year, v.Color, v.Status, v.CurrentKm))
            .ToListAsync();
    }

    public async Task<VehicleResponse?> GetVehicleByIdAsync(int id)
    {
        return await db.Vehicles
            .Include(v => v.VehicleType)
            .Where(v => v.VehicleId == id)
            .Select(v => new VehicleResponse(
                v.VehicleId, v.TypeId, v.VehicleType.TypeName, v.LicensePlate,
                v.Brand, v.Model, v.Year, v.Color, v.Status, v.CurrentKm))
            .FirstOrDefaultAsync();
    }

    public async Task<VehicleResponse?> CreateVehicleAsync(CreateVehicleRequest request)
    {
        var typeExists = await db.VehicleTypes.AnyAsync(vt => vt.TypeId == request.TypeId);
        if (!typeExists) return null;

        var vehicle = new Entities.Vehicle
        {
            TypeId = request.TypeId,
            LicensePlate = request.LicensePlate,
            Brand = request.Brand,
            Model = request.Model,
            Year = request.Year,
            Color = request.Color,
            Status = VehicleStatuses.Available,
            CurrentKm = request.CurrentKm,
            CreatedAt = DateTime.UtcNow
        };

        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        return await GetVehicleByIdAsync(vehicle.VehicleId);
    }

    public async Task<VehicleResponse?> UpdateVehicleAsync(int id, UpdateVehicleRequest request)
    {
        var vehicle = await db.Vehicles.FindAsync(id);
        if (vehicle is null) return null;

        vehicle.TypeId = request.TypeId;
        vehicle.LicensePlate = request.LicensePlate;
        vehicle.Brand = request.Brand;
        vehicle.Model = request.Model;
        vehicle.Year = request.Year;
        vehicle.Color = request.Color;
        vehicle.Status = request.Status;
        vehicle.CurrentKm = request.CurrentKm;

        await db.SaveChangesAsync();
        return await GetVehicleByIdAsync(id);
    }

    public async Task<bool> DeleteVehicleAsync(int id)
    {
        var vehicle = await db.Vehicles.FindAsync(id);
        if (vehicle is null) return false;

        var hasAssignment = await db.TripAssignments.AnyAsync(t => t.VehicleId == id);
        if (hasAssignment) return false;

        db.Vehicles.Remove(vehicle);
        await db.SaveChangesAsync();
        return true;
    }
}
