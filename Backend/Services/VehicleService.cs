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

    public async Task<List<AdminVehicleTypeResponse>> GetAdminVehicleTypesAsync()
    {
        var rows = await db.VehicleTypes
            .AsNoTracking()
            .OrderBy(vt => vt.TypeId)
            .ToListAsync();
        return rows.Select(MapAdmin).ToList();
    }

    public async Task<AdminVehicleTypeResponse?> GetAdminVehicleTypeAsync(int id)
    {
        var vt = await db.VehicleTypes.AsNoTracking().FirstOrDefaultAsync(x => x.TypeId == id);
        return vt is null ? null : MapAdmin(vt);
    }

    public async Task<(AdminVehicleTypeResponse? Data, string? Error, int StatusCode)> UpdatePricingAsync(
        int id, UpdateVehicleTypePricingRequest request)
    {
        var validation = ValidatePricing(request);
        if (validation is not null)
            return (null, validation, StatusCodes.Status400BadRequest);

        var vt = await db.VehicleTypes.FirstOrDefaultAsync(x => x.TypeId == id);
        if (vt is null)
            return (null, "Không tìm thấy loại xe.", StatusCodes.Status404NotFound);

        vt.PricePerDay = request.PricePerDay!.Value;
        vt.PricePerKm = request.PricePerKm!.Value;
        vt.DriverFeePerDay = request.DriverFeePerDay!.Value;
        vt.SelfDrivePricePerDay = request.SelfDrivePricePerDay!.Value;
        vt.SelfDriveIncludedKmPerDay = request.SelfDriveIncludedKmPerDay!.Value;
        vt.SelfDriveExtraKmPrice = request.SelfDriveExtraKmPrice!.Value;
        vt.WithDriverDepositAmount = request.WithDriverDepositAmount!.Value;
        vt.SelfDriveDepositAmount = request.SelfDriveDepositAmount!.Value;

        await db.SaveChangesAsync();
        return (MapAdmin(vt), null, StatusCodes.Status200OK);
    }

    internal static string? ValidatePricing(UpdateVehicleTypePricingRequest request)
    {
        if (request.PricePerDay is null
            || request.PricePerKm is null
            || request.DriverFeePerDay is null
            || request.SelfDrivePricePerDay is null
            || request.SelfDriveIncludedKmPerDay is null
            || request.SelfDriveExtraKmPrice is null
            || request.WithDriverDepositAmount is null
            || request.SelfDriveDepositAmount is null)
            return "Phải gửi đủ 8 giá, không được để trống.";

        if (request.PricePerDay < 0
            || request.PricePerKm < 0
            || request.DriverFeePerDay < 0
            || request.SelfDrivePricePerDay < 0
            || request.SelfDriveIncludedKmPerDay < 0
            || request.SelfDriveExtraKmPrice < 0
            || request.WithDriverDepositAmount < 0
            || request.SelfDriveDepositAmount < 0)
            return "Giá không được âm.";

        return null;
    }

    private static AdminVehicleTypeResponse MapAdmin(Entities.VehicleType vt) => new(
        vt.TypeId,
        vt.TypeName,
        vt.SeatCapacity,
        vt.PricePerDay,
        vt.PricePerKm,
        vt.DriverFeePerDay ?? 0,
        vt.SelfDrivePricePerDay ?? 0,
        vt.SelfDriveIncludedKmPerDay ?? 0,
        vt.SelfDriveExtraKmPrice ?? 0,
        vt.WithDriverDepositAmount ?? 0,
        vt.SelfDriveDepositAmount ?? 0,
        vt.Description,
        vt.ImageUrl,
        vt.IsActive);
}
