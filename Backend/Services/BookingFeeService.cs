using Backend.Constants;
using Backend.Data;
using Backend.Entities;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class BookingFeeService(CarRentalDbContext db, PricingService pricing)
{
    public async Task ApplyCompletionAsync(
        Booking booking,
        decimal? actualKm,
        VehicleInspection? handover,
        VehicleInspection returnInspection)
    {
        // Mid-trip vehicle change (plan1.md Phần I): km = sum of the legs, fuel compared with the last car at the change.
        var startFuel = handover?.FuelLevel;
        var changes = await db.VehicleChanges.AsNoTracking()
            .Where(c => c.BookingId == booking.BookingId)
            .Select(c => new { c.DuringTrip, c.PriceDifference })
            .ToListAsync();
        if (changes.Any(c => c.DuringTrip))
            (actualKm, startFuel) = await VehicleChangeService.ResolveTripAsync(db, booking.BookingId, handover, returnInspection.OdometerKm);

        var breakdown = pricing.CalculateFinalBreakdown(
            booking,
            actualKm,
            handover?.ActualAt,
            returnInspection.ActualAt,
            startFuel,
            returnInspection.FuelLevel);

        // Price difference of the vehicle changes the customer agreed to (positive = pays more, negative = refund).
        booking.FinalAmount = breakdown.FinalAmount is decimal final
            ? Math.Max(0, final + changes.Sum(c => c.PriceDifference))
            : null;

        if (breakdown.ExtraKmAmount > 0)
        {
            RentalModes.TryResolve(booking.RentalMode, out var mode);
            var extraPrice = mode == RentalModes.SelfDrive
                ? booking.QuotedSelfDriveExtraKmPrice ?? booking.QuotedPricePerKm
                : booking.QuotedPricePerKm;
            var description = mode == RentalModes.SelfDrive
                ? $"Km vượt hạn mức {breakdown.ExtraKm} km × {extraPrice} (đã gồm trong giá chốt)"
                : $"Cước km {breakdown.ExtraKm} km × {extraPrice} (đã gồm trong giá chốt)";
            await TryAddAsync(booking.BookingId, BookingFeeTypes.ExtraKm, description, breakdown.ExtraKmAmount);
        }

        if (breakdown.LateFeeAmount is > 0)
            await TryAddAsync(
                booking.BookingId,
                BookingFeeTypes.LateFee,
                $"Phí trả muộn {breakdown.LateDays} ngày",
                breakdown.LateFeeAmount.Value);

        if (breakdown.FuelFeeAmount is > 0)
            await TryAddAsync(
                booking.BookingId,
                BookingFeeTypes.Fuel,
                $"Nhiên liệu giảm {breakdown.FuelDrop}",
                breakdown.FuelFeeAmount.Value);
    }

    public async Task<bool> TryAddAsync(int bookingId, string feeType, string? description, decimal amount)
    {
        var error = BookingFeeRules.Validate(feeType, amount, description, out var resolvedType);
        if (error is not null || amount <= 0)
            return false;

        var duplicate = db.BookingFees.Local.Any(f => f.BookingId == bookingId && f.FeeType == resolvedType)
            || await db.BookingFees.AnyAsync(f => f.BookingId == bookingId && f.FeeType == resolvedType);
        if (duplicate)
            return false;

        db.BookingFees.Add(new BookingFee
        {
            BookingId = bookingId,
            FeeType = resolvedType,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Amount = amount,
            CreatedAt = DateTime.UtcNow
        });
        return true;
    }
}
