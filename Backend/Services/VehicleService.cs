using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Vehicles;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class VehicleService(CarRentalDbContext db, IRealtimePublisher? realtime = null)
{
    private readonly ScheduleConflictService schedule = new(db);
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
                v.Brand, v.Model, v.Year, v.Color, v.Status, v.CurrentKm,
                v.RegistrationNumber, v.RegistrationExpiryDate,
                v.InspectionExpiryDate, v.InsuranceExpiryDate))
            .ToListAsync();
    }

    public async Task<VehicleResponse?> GetVehicleByIdAsync(int id)
    {
        return await db.Vehicles
            .Include(v => v.VehicleType)
            .Where(v => v.VehicleId == id)
            .Select(v => new VehicleResponse(
                v.VehicleId, v.TypeId, v.VehicleType.TypeName, v.LicensePlate,
                v.Brand, v.Model, v.Year, v.Color, v.Status, v.CurrentKm,
                v.RegistrationNumber, v.RegistrationExpiryDate,
                v.InspectionExpiryDate, v.InsuranceExpiryDate))
            .FirstOrDefaultAsync();
    }

    public Task<(VehicleResponse? Data, string? Error, int StatusCode)> CreateVehicleAsync(
        CreateVehicleRequest request)
        => SqliteWriteLock.ExecuteAsync(db, () => CreateVehicleCoreAsync(request));

    private async Task<(VehicleResponse? Data, string? Error, int StatusCode)> CreateVehicleCoreAsync(
        CreateVehicleRequest request)
    {
        var plateError = VehicleLegalRules.ValidateLicensePlate(request.LicensePlate, out var licensePlate);
        if (plateError is not null)
            return (null, plateError, StatusCodes.Status400BadRequest);

        var yearError = VehicleLegalRules.ValidateYear(request.Year);
        if (yearError is not null)
            return (null, yearError, StatusCodes.Status400BadRequest);

        var legalError = VehicleLegalRules.ValidateMetadata(
            request.RegistrationNumber,
            request.RegistrationExpiryDate,
            request.InspectionExpiryDate,
            request.InsuranceExpiryDate,
            out var registration);
        if (legalError is not null)
            return (null, legalError, StatusCodes.Status400BadRequest);

        var typeExists = await db.VehicleTypes.AnyAsync(vt => vt.TypeId == request.TypeId);
        if (!typeExists)
            return (null, VehicleLegalRules.TypeNotFound, StatusCodes.Status400BadRequest);

        if (await LicensePlateTakenAsync(licensePlate!, excludeVehicleId: null))
            return (null, VehicleLegalRules.DuplicateLicensePlate, StatusCodes.Status400BadRequest);

        if (registration is not null && await RegistrationTakenAsync(registration, excludeVehicleId: null))
            return (null, VehicleLegalRules.DuplicateRegistration, StatusCodes.Status400BadRequest);

        var vehicle = new Entities.Vehicle
        {
            TypeId = request.TypeId,
            LicensePlate = licensePlate!,
            Brand = request.Brand,
            Model = request.Model,
            Year = request.Year,
            Color = request.Color,
            Status = VehicleStatuses.Available,
            CurrentKm = request.CurrentKm,
            RegistrationNumber = registration,
            RegistrationExpiryDate = request.RegistrationExpiryDate,
            InspectionExpiryDate = request.InspectionExpiryDate,
            InsuranceExpiryDate = request.InsuranceExpiryDate,
            CreatedAt = DateTime.UtcNow
        };

        db.Vehicles.Add(vehicle);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            db.Entry(vehicle).State = EntityState.Detached;
            return (null, VehicleLegalRules.DuplicateLicensePlate, StatusCodes.Status400BadRequest);
        }

        return (await GetVehicleByIdAsync(vehicle.VehicleId), null, StatusCodes.Status201Created);
    }

    public Task<(VehicleResponse? Data, string? Error, int StatusCode)> UpdateVehicleAsync(
        int id, UpdateVehicleRequest request)
        => SqliteWriteLock.ExecuteAsync(db, () => UpdateVehicleCoreAsync(id, request));

    private async Task<(VehicleResponse? Data, string? Error, int StatusCode)> UpdateVehicleCoreAsync(
        int id, UpdateVehicleRequest request)
    {
        var plateError = VehicleLegalRules.ValidateLicensePlate(request.LicensePlate, out var licensePlate);
        if (plateError is not null)
            return (null, plateError, StatusCodes.Status400BadRequest);

        var yearError = VehicleLegalRules.ValidateYear(request.Year);
        if (yearError is not null)
            return (null, yearError, StatusCodes.Status400BadRequest);

        var legalError = VehicleLegalRules.ValidateMetadata(
            request.RegistrationNumber,
            request.RegistrationExpiryDate,
            request.InspectionExpiryDate,
            request.InsuranceExpiryDate,
            out var registration);
        if (legalError is not null)
            return (null, legalError, StatusCodes.Status400BadRequest);

        var vehicle = await db.Vehicles.FindAsync(id);
        if (vehicle is null)
            return (null, VehicleLegalRules.VehicleNotFound, StatusCodes.Status404NotFound);

        if (await LicensePlateTakenAsync(licensePlate!, excludeVehicleId: id))
            return (null, VehicleLegalRules.DuplicateLicensePlate, StatusCodes.Status400BadRequest);

        if (registration is not null && await RegistrationTakenAsync(registration, excludeVehicleId: id))
            return (null, VehicleLegalRules.DuplicateRegistration, StatusCodes.Status400BadRequest);

        if (request.Status == VehicleStatuses.Inactive
            && vehicle.Status != VehicleStatuses.Inactive
            && await schedule.IsVehicleOccupiedAsync(id))
            return (null, VehicleOccupancyRules.CannotDeactivate, StatusCodes.Status400BadRequest);

        if (request.Status == VehicleStatuses.Available
            && vehicle.Status == VehicleStatuses.Rented)
            return (null, VehicleOccupancyRules.CannotReleaseRented, StatusCodes.Status400BadRequest);

        if (request.Status == VehicleStatuses.Rented
            && vehicle.Status != VehicleStatuses.Rented)
            return (null, VehicleOccupancyRules.CannotSetRentedManually, StatusCodes.Status400BadRequest);

        vehicle.TypeId = request.TypeId;
        vehicle.LicensePlate = licensePlate!;
        vehicle.Brand = request.Brand;
        vehicle.Model = request.Model;
        vehicle.Year = request.Year;
        vehicle.Color = request.Color;
        vehicle.Status = request.Status;
        vehicle.CurrentKm = request.CurrentKm;
        vehicle.RegistrationNumber = registration;
        vehicle.RegistrationExpiryDate = request.RegistrationExpiryDate;
        vehicle.InspectionExpiryDate = request.InspectionExpiryDate;
        vehicle.InsuranceExpiryDate = request.InsuranceExpiryDate;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            db.Entry(vehicle).State = EntityState.Detached;
            return (null, VehicleLegalRules.DuplicateLicensePlate, StatusCodes.Status400BadRequest);
        }

        await RealtimeNotify.VehicleStatusChanged(realtime, vehicle.VehicleId, vehicle.Status);
        return (await GetVehicleByIdAsync(id), null, StatusCodes.Status200OK);
    }

    private async Task<bool> LicensePlateTakenAsync(string licensePlate, int? excludeVehicleId)
    {
        var query = db.Vehicles.AsQueryable();
        if (excludeVehicleId is int id)
            query = query.Where(v => v.VehicleId != id);

        var normalized = licensePlate.ToLower();
        return await query.AnyAsync(v => v.LicensePlate.Trim().ToLower() == normalized);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        for (Exception? inner = ex.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is Microsoft.Data.Sqlite.SqliteException sqlite && sqlite.SqliteErrorCode == 19)
                return true;

            if (inner.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
                || inner.Message.Contains("UNIQUE KEY constraint", StringComparison.OrdinalIgnoreCase)
                || inner.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private async Task<bool> RegistrationTakenAsync(string registration, int? excludeVehicleId)
    {
        var query = db.Vehicles.Where(v => v.RegistrationNumber != null);
        if (excludeVehicleId is int id)
            query = query.Where(v => v.VehicleId != id);

        var normalized = registration.ToLower();
        return await query.AnyAsync(v => v.RegistrationNumber!.ToLower() == normalized);
    }

    public Task<(bool Ok, string? Error, int StatusCode)> DeleteVehicleAsync(int id)
        => SqliteWriteLock.ExecuteAsync(db, () => DeleteVehicleCoreAsync(id));

    private async Task<(bool Ok, string? Error, int StatusCode)> DeleteVehicleCoreAsync(int id)
    {
        var vehicle = await db.Vehicles.FindAsync(id);
        if (vehicle is null)
            return (false, VehicleLegalRules.VehicleNotFound, StatusCodes.Status404NotFound);

        if (await schedule.IsVehicleOccupiedAsync(id))
            return (false, VehicleOccupancyRules.CannotDelete, StatusCodes.Status400BadRequest);

        var hasAssignment = await db.TripAssignments.AnyAsync(t => t.VehicleId == id);
        if (hasAssignment)
            return (false, VehicleOccupancyRules.CannotDeleteHistoric, StatusCodes.Status400BadRequest);

        db.Vehicles.Remove(vehicle);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return (false, VehicleOccupancyRules.CannotDeleteHistoric, StatusCodes.Status400BadRequest);
        }

        return (true, null, StatusCodes.Status204NoContent);
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

    public async Task<(AdminVehicleTypeResponse? Data, string? Error, int StatusCode)> CreateTypeAsync(
        CreateVehicleTypeRequest request)
    {
        var name = (request.TypeName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return (null, "Vui lòng nhập tên loại xe.", StatusCodes.Status400BadRequest);

        if (request.PricePerDay < 0 || request.PricePerKm < 0)
            return (null, "Giá không được âm.", StatusCodes.Status400BadRequest);

        var vt = new Entities.VehicleType
        {
            TypeName = name,
            SeatCapacity = 4,
            PricePerDay = request.PricePerDay,
            PricePerKm = request.PricePerKm,
            DriverFeePerDay = PricingDefaults.DriverFeePerDay(request.PricePerDay),
            SelfDrivePricePerDay = PricingDefaults.SelfDrivePricePerDay(request.PricePerDay),
            SelfDriveIncludedKmPerDay = PricingDefaults.IncludedKmPerDay,
            SelfDriveExtraKmPrice = PricingDefaults.ExtraKmPrice(request.PricePerKm),
            WithDriverDepositAmount = PricingDefaults.WithDriverDeposit(request.PricePerDay),
            SelfDriveDepositAmount = PricingDefaults.SelfDriveDeposit(request.PricePerDay),
            IsActive = true
        };

        db.VehicleTypes.Add(vt);
        await db.SaveChangesAsync();
        return (MapAdmin(vt), null, StatusCodes.Status201Created);
    }

    public async Task<(bool Ok, string? Error, int StatusCode)> DeleteTypeAsync(int id)
    {
        var vt = await db.VehicleTypes.FirstOrDefaultAsync(x => x.TypeId == id);
        if (vt is null)
            return (false, "Không tìm thấy loại xe.", StatusCodes.Status404NotFound);

        var hasVehicles = await db.Vehicles.AnyAsync(v => v.TypeId == id);
        if (hasVehicles)
            return (false, "Không thể xóa loại xe đang có phương tiện.", StatusCodes.Status400BadRequest);

        db.VehicleTypes.Remove(vt);
        await db.SaveChangesAsync();
        return (true, null, StatusCodes.Status200OK);
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
