using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Bookings;
using Backend.Entities;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class VehicleInspectionService(CarRentalDbContext db)
{
    public async Task<(VehicleInspection? Inspection, string? Error)> AddAsync(
        int bookingId,
        string? inspectionType,
        decimal? odometerKm,
        decimal? fuelLevel,
        string? condition,
        string? notes,
        string? exteriorCondition = null,
        string? technicalCondition = null,
        int? actorUserId = null)
    {
        var error = VehicleInspectionRules.Validate(
            inspectionType, odometerKm, fuelLevel, condition, notes, out var resolvedType,
            exteriorCondition, technicalCondition);
        if (error is not null)
            return (null, error);

        var booking = await db.Bookings
            .Include(b => b.TripAssignment)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);
        if (booking is null)
            return (null, "Không tìm thấy đơn.");

        var vehicleId = booking.AssignedVehicleId ?? booking.TripAssignment?.VehicleId;
        if (vehicleId is null or <= 0)
            return (null, "Đơn chưa được gán xe.");

        var duplicate = await db.VehicleInspections.AnyAsync(i =>
            i.BookingId == bookingId && i.InspectionType == resolvedType);
        if (duplicate)
            return (null, resolvedType switch
            {
                VehicleInspectionTypes.PreHandover => "Đơn này đã có biên bản kiểm tra trước giao.",
                VehicleInspectionTypes.Handover => "Đơn này đã có biên bản giao xe.",
                _ => "Đơn này đã có biên bản trả xe."
            });

        var vehicle = await db.Vehicles.FindAsync(vehicleId.Value);
        if (vehicle is null)
            return (null, "Không tìm thấy xe đã gán.");

        if (resolvedType == VehicleInspectionTypes.PreHandover)
        {
            error = ValidatePreHandover(odometerKm, exteriorCondition ?? condition, technicalCondition, vehicle.CurrentKm);
            if (error is not null)
                return (null, error);
        }

        if (resolvedType == VehicleInspectionTypes.Return)
        {
            error = VehicleInspectionRules.ValidateReturnOdometer(odometerKm, vehicle.CurrentKm);
            if (error is not null)
                return (null, error);

            var handoverOdo = await db.VehicleInspections
                .Where(i => i.BookingId == bookingId && i.InspectionType == VehicleInspectionTypes.Handover)
                .Select(i => i.OdometerKm)
                .FirstOrDefaultAsync();
            // After a mid-trip vehicle change the current car started at its reading at the change (plan1.md Phần I).
            var segmentStart = await VehicleChangeService.SegmentStartKmAsync(db, bookingId, handoverOdo);
            error = VehicleInspectionRules.ValidateReturnVsHandover(odometerKm, segmentStart);
            if (error is not null)
                return (null, error);

            if (odometerKm is not null)
            {
                var km = (int)Math.Round(odometerKm.Value, 0, MidpointRounding.AwayFromZero);
                if (km < vehicle.CurrentKm)
                    return (null, VehicleInspectionRules.OdometerBelowCurrent);
                vehicle.CurrentKm = km;
            }
        }

        var actor = await ActorLookup.FindAsync(db, actorUserId);
        var now = DateTime.UtcNow;
        var exterior = TrimOrNull(exteriorCondition) ?? TrimOrNull(condition);
        var technical = TrimOrNull(technicalCondition);
        var inspection = new VehicleInspection
        {
            BookingId = bookingId,
            VehicleId = vehicleId.Value,
            InspectionType = resolvedType,
            ActualAt = now,
            OdometerKm = odometerKm,
            FuelLevel = fuelLevel,
            Condition = TrimOrNull(condition) ?? exterior,
            ExteriorCondition = exterior,
            TechnicalCondition = technical,
            Notes = TrimOrNull(notes),
            CreatedAt = now,
            CreatedByUserId = actor?.UserId,
            CreatedByName = actor?.Name,
            CreatedByRole = actor?.Role
        };

        db.VehicleInspections.Add(inspection);
        return (inspection, null);
    }

    /// <summary>
    /// Dispatcher corrects a PreHandover after a driver dispute. Same rules as creating one;
    /// records who edited it. The caller checks that the driver has not confirmed it yet.
    /// </summary>
    public async Task<string?> UpdatePreHandoverAsync(
        VehicleInspection inspection, VehicleConditionRequest request, int? actorUserId)
    {
        var error = VehicleInspectionRules.Validate(
            VehicleInspectionTypes.PreHandover, request.OdometerKm, request.FuelLevel, request.Condition, request.Notes,
            out _, request.ExteriorCondition, request.TechnicalCondition);
        if (error is not null)
            return error;
        var vehicle = await db.Vehicles.FindAsync(inspection.VehicleId);
        error = ValidatePreHandover(
            request.OdometerKm, request.ExteriorCondition ?? request.Condition, request.TechnicalCondition, vehicle?.CurrentKm ?? 0);
        if (error is not null)
            return error;

        var actor = await ActorLookup.FindAsync(db, actorUserId);
        var exterior = TrimOrNull(request.ExteriorCondition) ?? TrimOrNull(request.Condition);
        inspection.OdometerKm = request.OdometerKm;
        inspection.FuelLevel = request.FuelLevel;
        inspection.Condition = TrimOrNull(request.Condition) ?? exterior;
        inspection.ExteriorCondition = exterior;
        inspection.TechnicalCondition = TrimOrNull(request.TechnicalCondition);
        inspection.Notes = TrimOrNull(request.Notes);
        inspection.ActualAt = DateTime.UtcNow;
        inspection.UpdatedAt = DateTime.UtcNow;
        inspection.UpdatedByUserId = actor?.UserId;
        inspection.UpdatedByName = actor?.Name;
        inspection.UpdatedByRole = actor?.Role;
        return null;
    }

    public Task<VehicleInspection?> GetPreHandoverAsync(int bookingId)
        => db.VehicleInspections.FirstOrDefaultAsync(i =>
            i.BookingId == bookingId && i.InspectionType == VehicleInspectionTypes.PreHandover);

    private static string? ValidatePreHandover(decimal? odometerKm, string? exterior, string? technical, int currentKm)
    {
        if (string.IsNullOrWhiteSpace(exterior) || string.IsNullOrWhiteSpace(technical))
            return VehicleInspectionRules.PreHandoverConditionRequired;
        if (odometerKm is not null && odometerKm < currentKm)
            return VehicleInspectionRules.PreHandoverOdometerBelowCurrent;
        return null;
    }

    public async Task<decimal?> GetHandoverOdometerAsync(int bookingId)
        => (await GetHandoverAsync(bookingId))?.OdometerKm;

    public async Task<VehicleInspection?> GetHandoverAsync(int bookingId)
        => await db.VehicleInspections
            .AsNoTracking()
            .FirstOrDefaultAsync(i =>
                i.BookingId == bookingId && i.InspectionType == VehicleInspectionTypes.Handover);

    public async Task<IReadOnlyList<VehicleInspectionResponse>> GetByBookingAsync(int bookingId)
    {
        var rows = await db.VehicleInspections
            .AsNoTracking()
            .Where(i => i.BookingId == bookingId)
            .OrderBy(i => i.InspectionId)
            .ToListAsync();

        return rows.Select(ToResponse).ToList();
    }

    public async Task<IReadOnlyList<VehicleInspectionResponse>> GetAdminAsync(int? bookingId)
    {
        var query = db.VehicleInspections.AsNoTracking().AsQueryable();
        if (bookingId is not null)
            query = query.Where(i => i.BookingId == bookingId.Value);

        var rows = await query.OrderBy(i => i.InspectionId).ToListAsync();
        return rows.Select(ToResponse).ToList();
    }

    /// <summary>
    /// Latest known tank level for a vehicle from inspection history (Return preferred, else any).
    /// Returns null when the vehicle has never recorded a valid FuelLevel — callers must not invent 0.
    /// </summary>
    public async Task<decimal?> GetLatestFuelLevelAsync(int vehicleId)
    {
        var rows = await db.VehicleInspections
            .AsNoTracking()
            .Where(i => i.VehicleId == vehicleId && i.FuelLevel != null)
            .OrderByDescending(i => i.InspectionId)
            .Select(i => new { i.InspectionType, i.FuelLevel })
            .ToListAsync();

        if (rows.Count == 0)
            return null;

        var latestReturn = rows.FirstOrDefault(i => i.InspectionType == VehicleInspectionTypes.Return);
        var fuel = latestReturn?.FuelLevel ?? rows[0].FuelLevel;
        if (fuel is < 0 or > 100)
            return null;
        return fuel;
    }

    public static VehicleInspectionResponse ToResponse(VehicleInspection i) => new(
        i.InspectionId,
        i.BookingId,
        i.VehicleId,
        i.InspectionType,
        i.ActualAt,
        i.OdometerKm,
        i.FuelLevel,
        i.Condition,
        i.Notes,
        i.CreatedAt,
        i.ExteriorCondition,
        i.TechnicalCondition,
        i.CreatedByUserId,
        i.CreatedByName,
        i.CreatedByRole,
        i.UpdatedByName,
        i.UpdatedByRole,
        i.UpdatedAt,
        i.VerificationStatus,
        i.VerificationNote,
        i.VerifiedByName,
        i.VerifiedAt);

    private static string? TrimOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
