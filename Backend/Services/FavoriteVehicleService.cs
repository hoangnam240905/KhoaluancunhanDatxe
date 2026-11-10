using Backend.Data;
using Backend.DTOs.Vehicles;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>Customer's favourite cars. Add and remove are idempotent, so a double click is harmless.</summary>
public class FavoriteVehicleService(CarRentalDbContext db)
{
    /// <summary>Favourite cars, most recently added first.</summary>
    public async Task<List<VehicleResponse>> GetAsync(int customerId)
    {
        var ids = await db.FavoriteVehicles.AsNoTracking()
            .Where(f => f.CustomerId == customerId)
            .OrderByDescending(f => f.FavoriteVehicleId)
            .Select(f => f.VehicleId)
            .ToListAsync();
        var vehicles = await db.Vehicles.AsNoTracking()
            .Where(v => ids.Contains(v.VehicleId))
            .Include(v => v.VehicleType)
            .Include(v => v.Images)
            .ToDictionaryAsync(v => v.VehicleId);
        return ids.Where(vehicles.ContainsKey).Select(id => VehicleService.MapVehicle(vehicles[id])).ToList();
    }

    /// <summary>False when the car does not exist.</summary>
    public Task<bool> AddAsync(int customerId, int vehicleId)
        => SqliteWriteLock.ExecuteAsync(db, async () =>
        {
            if (!await db.Vehicles.AnyAsync(v => v.VehicleId == vehicleId))
                return false;
            if (await db.FavoriteVehicles.AnyAsync(f => f.CustomerId == customerId && f.VehicleId == vehicleId))
                return true;
            db.FavoriteVehicles.Add(new FavoriteVehicle
            {
                CustomerId = customerId,
                VehicleId = vehicleId,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            return true;
        });

    public Task<int> RemoveAsync(int customerId, int vehicleId)
        => SqliteWriteLock.ExecuteAsync(db, () =>
            db.FavoriteVehicles.Where(f => f.CustomerId == customerId && f.VehicleId == vehicleId).ExecuteDeleteAsync());
}
