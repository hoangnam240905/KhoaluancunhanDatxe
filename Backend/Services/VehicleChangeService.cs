using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Dispatch;
using Backend.Entities;
using Backend.Services.Notifications;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>
/// Changing the car of a booking (plan1.md Phần I, contract terms 10–13), done by a dispatcher before pickup
/// or during the trip. Same-type cars first; another type only when none is free, and then only if the
/// customer agreed (by phone) to the price difference. Every change is a contract appendix with the
/// dispatcher's name. A mid-trip change records both cars' readings; the trip's km is the sum of the legs.
/// </summary>
public class VehicleChangeService(CarRentalDbContext db, VehicleInspectionService inspections)
{
    private readonly ScheduleConflictService schedule = new(db);

    public const string NotFound = "Không tìm thấy đơn.";
    public const string WrongStatus = "Chỉ đổi xe cho đơn đã phân công xe hoặc đang thuê.";
    public const string NoCurrentVehicle = "Đơn chưa có xe để đổi.";
    public const string ReasonInvalid = "Vui lòng chọn lý do đổi xe.";
    public const string NoteTooLong = "Ghi chú tối đa 500 ký tự.";
    public const string VehicleNotAvailable = "Xe thay thế không còn trống lịch hoặc không phù hợp với đơn.";
    public const string SameTypeFirst = "Còn xe cùng loại trống lịch, vui lòng chọn xe cùng loại trước.";
    public const string AgreementRequired = "Xe khác loại có chênh lệch giá: cần xác nhận khách đã đồng ý trước khi đổi.";
    public const string ReadingsRequired = "Đổi xe giữa chuyến: nhập số km và mức nhiên liệu của cả xe cũ và xe mới.";
    public const string FuelInvalid = "Mức nhiên liệu phải từ 0 đến 100.";
    public const string OldKmTooLow = "Số km xe cũ không được nhỏ hơn số km lúc nhận xe.";
    public const string NewKmTooLow = "Số km xe mới không được nhỏ hơn số km hiện tại của xe.";

    public async Task<(VehicleChangeOptionsResponse? Data, string? Error)> GetOptionsAsync(int bookingId)
    {
        var ctx = await LoadAsync(bookingId);
        if (ctx.Error is not null)
            return (null, ctx.Error);
        var c = ctx.Value!;
        var same = await CandidatesAsync(c, c.CurrentType);
        var others = new List<VehicleChangeOption>();
        if (same.Count == 0)
        {
            var types = await db.VehicleTypes.AsNoTracking()
                .Where(t => t.IsActive && t.TypeId != c.CurrentType.TypeId)
                .OrderBy(t => t.SeatCapacity)
                .ToListAsync();
            foreach (var type in types)
                others.AddRange(await CandidatesAsync(c, type));
        }
        return (new VehicleChangeOptionsResponse(
            c.Booking.BookingId, c.Current.VehicleId, $"{c.Current.Brand} {c.Current.Model} · {c.Current.LicensePlate}".Trim(),
            c.CurrentType.TypeName, c.DuringTrip, c.RemainingDays, c.SegmentStartKm, same, others), null);
    }

    /// <summary>
    /// Runs under the same write lock as assignment / payment (SqliteWriteLock): the free-car check and the switch
    /// happen in one transaction, so two dispatchers cannot move two bookings onto the same car (no double-booking).
    /// </summary>
    public Task<(VehicleChangeResponse? Data, string? Error)> ApplyAsync(int bookingId, int dispatcherId, ChangeVehicleRequest request)
        => SqliteWriteLock.ExecuteAsync(db, () => ApplyCoreAsync(bookingId, dispatcherId, request));

    private async Task<(VehicleChangeResponse? Data, string? Error)> ApplyCoreAsync(int bookingId, int dispatcherId, ChangeVehicleRequest request)
    {
        if (!VehicleChangeReasons.IsValid(request.Reason))
            return (null, ReasonInvalid);
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is { Length: > 500 })
            return (null, NoteTooLong);

        var (options, error) = await GetOptionsAsync(bookingId);
        if (options is null)
            return (null, error);
        var chosen = options.SameType.FirstOrDefault(v => v.VehicleId == request.NewVehicleId)
            ?? options.OtherTypes.FirstOrDefault(v => v.VehicleId == request.NewVehicleId);
        if (chosen is null)
        {
            var exists = await db.Vehicles.AnyAsync(v => v.VehicleId == request.NewVehicleId);
            return (null, exists && options.SameType.Count > 0 ? SameTypeFirst : VehicleNotAvailable);
        }
        if (chosen.PriceDifference != 0 && !request.CustomerAgreed)
            return (null, AgreementRequired);

        if (options.DuringTrip)
        {
            if (request.OldOdometerKm is null || request.OldFuelLevel is null || request.NewOdometerKm is null || request.NewFuelLevel is null)
                return (null, ReadingsRequired);
            if (request.OldFuelLevel is < 0 or > 100 || request.NewFuelLevel is < 0 or > 100)
                return (null, FuelInvalid);
            if (options.SegmentStartKm is decimal start && request.OldOdometerKm < start)
                return (null, OldKmTooLow);
            if (request.NewOdometerKm < chosen.CurrentKm)
                return (null, NewKmTooLow);
        }

        var booking = await db.Bookings.Include(b => b.TripAssignment).FirstAsync(b => b.BookingId == bookingId);
        var oldVehicle = await db.Vehicles.FirstAsync(v => v.VehicleId == options.CurrentVehicleId);
        var newVehicle = await db.Vehicles.Include(v => v.VehicleType).FirstAsync(v => v.VehicleId == chosen.VehicleId);
        var actor = await ActorLookup.FindAsync(db, dispatcherId);

        // Move the booking (and the trip) to the new car.
        if (booking.AssignedVehicleId is not null || booking.TripAssignment is null)
            booking.AssignedVehicleId = newVehicle.VehicleId;
        if (booking.TripAssignment is { } trip)
            trip.VehicleId = newVehicle.VehicleId;
        // Another type (only when no same-type car was free): the booking now rents that type; the price
        // snapshot stays and the agreed difference is added at completion.
        booking.VehicleTypeId = newVehicle.TypeId;
        booking.UpdatedAt = DateTime.UtcNow;
        newVehicle.Status = VehicleStatuses.Rented;

        var preHandoverReset = false;
        if (!options.DuringTrip && booking.TripAssignment is not null)
        {
            // The dispatcher's pre-handover check was about the old car (plan1.md Phần A): record it again.
            var pre = await db.VehicleInspections.FirstOrDefaultAsync(i =>
                i.BookingId == bookingId && i.InspectionType == VehicleInspectionTypes.PreHandover);
            if (pre is not null && await inspections.GetHandoverAsync(bookingId) is null)
            {
                db.VehicleInspections.Remove(pre);
                preHandoverReset = true;
            }
        }

        if (options.DuringTrip && request.OldOdometerKm is decimal oldKm)
            oldVehicle.CurrentKm = Math.Max(oldVehicle.CurrentKm, (int)Math.Round(oldKm, MidpointRounding.AwayFromZero));
        if (options.DuringTrip && request.NewOdometerKm is decimal newKm)
            newVehicle.CurrentKm = Math.Max(newVehicle.CurrentKm, (int)Math.Round(newKm, MidpointRounding.AwayFromZero));

        var change = new VehicleChange
        {
            BookingId = bookingId,
            OldVehicleId = oldVehicle.VehicleId,
            NewVehicleId = newVehicle.VehicleId,
            OldTypeId = oldVehicle.TypeId,
            NewTypeId = newVehicle.TypeId,
            Reason = request.Reason!,
            Note = note,
            DuringTrip = options.DuringTrip,
            OldOdometerKm = options.DuringTrip ? request.OldOdometerKm : null,
            OldFuelLevel = options.DuringTrip ? request.OldFuelLevel : null,
            NewOdometerKm = options.DuringTrip ? request.NewOdometerKm : null,
            NewFuelLevel = options.DuringTrip ? request.NewFuelLevel : null,
            RemainingDays = options.RemainingDays,
            PriceDifference = chosen.PriceDifference,
            CustomerAgreed = chosen.PriceDifference != 0 && request.CustomerAgreed,
            ApprovedByUserId = actor?.UserId,
            ApprovedByName = actor?.Name,
            CreatedAt = DateTime.UtcNow
        };
        db.VehicleChanges.Add(change);
        if (chosen.PriceDifference > 0)
            db.BookingFees.Add(new BookingFee
            {
                BookingId = bookingId,
                FeeType = BookingFeeTypes.VehicleChange,
                Description = $"Chênh lệch đổi xe {oldVehicle.LicensePlate} → {newVehicle.LicensePlate} ({options.RemainingDays} ngày còn lại)",
                Amount = chosen.PriceDifference,
                CreatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        // The old car: to maintenance when it broke down, otherwise free again when nothing else holds it.
        if (request.Reason == VehicleChangeReasons.Breakdown)
            oldVehicle.Status = VehicleStatuses.Maintenance;
        else
            await VehicleOccupancy.ReleaseIfIdleAsync(db, oldVehicle.VehicleId, bookingId);

        var newText = $"{newVehicle.Brand} {newVehicle.Model} ({newVehicle.LicensePlate})".Trim();
        var diffText = chosen.PriceDifference switch
        {
            > 0 => $" Chênh lệch giá: +{chosen.PriceDifference:#,0} ₫.",
            < 0 => $" Chênh lệch giá: {chosen.PriceDifference:#,0} ₫ (hoàn lại theo quy định).",
            _ => ""
        };
        UserNotifications.Add(db, booking.CustomerId, $"Đơn #{bookingId} đã được đổi xe",
            $"Xe mới của bạn: {newText}.{diffText} Xem phụ lục đổi xe trong chi tiết đơn.", NotificationRules.TypeBooking);
        if (booking.TripAssignment is { } assigned && assigned.Status != TripAssignmentStatuses.Cancelled)
            UserNotifications.Add(db, assigned.DriverId, $"Chuyến #{bookingId} đổi sang xe {newVehicle.LicensePlate}",
                preHandoverReset
                    ? $"Xe mới: {newText}. Điều phối sẽ ghi lại biên bản kiểm tra trước giao cho xe mới."
                    : $"Xe mới: {newText}.", NotificationRules.TypeDriver);
        await db.SaveChangesAsync();
        return ((await ListAsync(bookingId)).First(x => x.VehicleChangeId == change.VehicleChangeId), null);
    }

    public async Task<List<VehicleChangeResponse>> ListAsync(int bookingId)
    {
        var rows = await db.VehicleChanges.AsNoTracking()
            .Include(c => c.OldVehicle).ThenInclude(v => v.VehicleType)
            .Include(c => c.NewVehicle).ThenInclude(v => v.VehicleType)
            .Where(c => c.BookingId == bookingId)
            .OrderBy(c => c.VehicleChangeId)
            .ToListAsync();
        return rows.Select(c => new VehicleChangeResponse(
            c.VehicleChangeId, c.BookingId,
            $"{c.OldVehicle.Brand} {c.OldVehicle.Model} · {c.OldVehicle.LicensePlate}".Trim(),
            $"{c.NewVehicle.Brand} {c.NewVehicle.Model} · {c.NewVehicle.LicensePlate}".Trim(),
            c.OldVehicle.VehicleType.TypeName, c.NewVehicle.VehicleType.TypeName,
            c.Reason, c.Note, c.DuringTrip, c.OldOdometerKm, c.OldFuelLevel, c.NewOdometerKm, c.NewFuelLevel,
            c.RemainingDays, c.PriceDifference, c.CustomerAgreed, c.ApprovedByName, c.CreatedAt)).ToList();
    }

    /// <summary>
    /// Trip km over several cars: (old car at change − handover) + … + (return − last new car at change).
    /// Also the fuel level the last car started with. No mid-trip change → plain return − handover.
    /// </summary>
    public static async Task<(decimal? ActualKm, decimal? StartFuel)> ResolveTripAsync(
        CarRentalDbContext db, int bookingId, VehicleInspection? handover, decimal? returnKm)
    {
        var legs = await db.VehicleChanges.AsNoTracking()
            .Where(c => c.BookingId == bookingId && c.DuringTrip)
            .OrderBy(c => c.VehicleChangeId)
            .ToListAsync();
        if (legs.Count == 0)
            return (VehicleInspectionRules.ResolveActualKm(handover?.OdometerKm, returnKm), handover?.FuelLevel);
        if (handover?.OdometerKm is not decimal start || returnKm is not decimal end)
            return (null, legs[^1].NewFuelLevel);

        decimal total = 0;
        var legStart = start;
        foreach (var leg in legs)
        {
            total += Math.Max(0, (leg.OldOdometerKm ?? legStart) - legStart);
            legStart = leg.NewOdometerKm ?? legStart;
        }
        total += Math.Max(0, end - legStart);
        return (total, legs[^1].NewFuelLevel);
    }

    /// <summary>Km the current car started this booking with: last mid-trip change, else the handover.</summary>
    public static async Task<decimal?> SegmentStartKmAsync(CarRentalDbContext db, int bookingId, decimal? handoverKm)
    {
        var last = await db.VehicleChanges.AsNoTracking()
            .Where(c => c.BookingId == bookingId && c.DuringTrip)
            .OrderByDescending(c => c.VehicleChangeId)
            .Select(c => c.NewOdometerKm)
            .FirstOrDefaultAsync();
        return last ?? handoverKm;
    }

    // ---------- helpers ----------

    private sealed record Context(Booking Booking, Vehicle Current, VehicleType CurrentType, Customer Customer,
        Driver? Driver, bool DuringTrip, DateTime From, DateTime Until, int RemainingDays, decimal? SegmentStartKm);

    private async Task<(Context? Value, string? Error)> LoadAsync(int bookingId)
    {
        var booking = await db.Bookings.AsNoTracking()
            .Include(b => b.TripAssignment).ThenInclude(t => t!.Driver)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);
        if (booking is null)
            return (null, NotFound);
        if (booking.Status is not (BookingStatuses.Assigned or BookingStatuses.InProgress))
            return (null, WrongStatus);
        var currentId = booking.AssignedVehicleId ?? booking.TripAssignment?.VehicleId;
        if (currentId is null)
            return (null, NoCurrentVehicle);
        var current = await db.Vehicles.AsNoTracking().Include(v => v.VehicleType).FirstAsync(v => v.VehicleId == currentId.Value);
        var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.CustomerId == booking.CustomerId);
        await LicenceBook.LoadAsync(db, customer);
        if (booking.TripAssignment?.Driver is { } assignedDriver)
            await LicenceBook.LoadAsync(db, assignedDriver);
        var duringTrip = booking.Status == BookingStatuses.InProgress;
        var now = VietnamTime.Now;
        var from = now > booking.StartDate ? now : booking.StartDate;
        // Overdue booking (return date already passed): the new car must at least be free for the next day.
        var until = booking.EndDate > from ? booking.EndDate : from.AddDays(1);
        var remainingDays = Math.Max(1, (int)Math.Ceiling((booking.EndDate - from).TotalHours / 24.0));
        decimal? segmentStart = null;
        if (duringTrip)
            segmentStart = await SegmentStartKmAsync(db, bookingId, (await inspections.GetHandoverAsync(bookingId))?.OdometerKm);
        return (new Context(booking, current, current.VehicleType, customer, booking.TripAssignment?.Driver,
            duringTrip, from, until, remainingDays, segmentStart), null);
    }

    private async Task<List<VehicleChangeOption>> CandidatesAsync(Context c, VehicleType type)
    {
        var selfDrive = RentalModes.TryResolve(c.Booking.RentalMode, out var mode) && mode == RentalModes.SelfDrive;
        // The new type must suit the person driving: the customer's licence (self-drive) or the driver's.
        // Phase 9: any of the customer's licences (not rejected) whose class covers the new type.
        if (selfDrive && !LicenceBook.Of(c.Customer).Any(l => l.Status != PersonLicenseStatuses.Rejected
                && LicenseClasses.CannotDrive(l.Class, type.MinLicenseClass, type.SeatCapacity, professional: false) is null))
            return [];
        if (!selfDrive && c.Driver is not null && DriverQualification.ReasonFor(c.Driver, type, c.Until) is not null)
            return [];

        var vehicles = await schedule.FindAssignableVehiclesAsync(type.TypeId, c.From, c.Until, c.Current.VehicleId, c.Booking.BookingId);
        var diff = (DayPrice(type, selfDrive) - DayPrice(c.CurrentType, selfDrive)) * c.RemainingDays;
        return vehicles
            .Where(v => v.VehicleId != c.Current.VehicleId)
            .Select(v => new VehicleChangeOption(
                v.VehicleId, v.LicensePlate, v.Brand, v.Model, type.TypeId, type.TypeName, type.SeatCapacity, v.CurrentKm,
                type.TypeId == c.CurrentType.TypeId, type.TypeId == c.CurrentType.TypeId ? 0 : diff))
            .ToList();
    }

    private static decimal DayPrice(VehicleType type, bool selfDrive)
        => selfDrive ? type.SelfDrivePricePerDay ?? type.PricePerDay : type.PricePerDay + (type.DriverFeePerDay ?? 0);
}
