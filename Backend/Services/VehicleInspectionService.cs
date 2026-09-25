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
        string? technicalCondition = null)
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
            return (null, resolvedType == VehicleInspectionTypes.Handover
                ? "Đơn này đã có biên bản giao xe."
                : "Đơn này đã có biên bản trả xe.");

        var vehicle = await db.Vehicles.FindAsync(vehicleId.Value);
        if (vehicle is null)
            return (null, "Không tìm thấy xe đã gán.");

        if (resolvedType == VehicleInspectionTypes.Return)
        {
            error = VehicleInspectionRules.ValidateReturnOdometer(odometerKm, vehicle.CurrentKm);
            if (error is not null)
                return (null, error);

            var handoverOdo = await db.VehicleInspections
                .Where(i => i.BookingId == bookingId && i.InspectionType == VehicleInspectionTypes.Handover)
                .Select(i => i.OdometerKm)
                .FirstOrDefaultAsync();
            error = VehicleInspectionRules.ValidateReturnVsHandover(odometerKm, handoverOdo);
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
            CreatedAt = now
        };

        db.VehicleInspections.Add(inspection);
        return (inspection, null);
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
        i.TechnicalCondition);

    private static string? TrimOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
