using Backend.Constants;
using Backend.Entities;

namespace Backend.Services;

public record PricingQuote(
    decimal QuotedPricePerDay,
    decimal QuotedPricePerKm,
    int QuotedDays,
    decimal EstimatedDistance,
    decimal RentalAmount,
    decimal DriverAmount,
    decimal DistanceAmount,
    decimal IncludedKm,
    decimal ExtraKm,
    decimal ExtraKmPrice,
    decimal DepositAmount,
    decimal QuotedDriverFeePerDay,
    decimal QuotedSelfDriveIncludedKmPerDay,
    decimal QuotedSelfDriveExtraKmPrice,
    decimal TotalAmount);

public class PricingService
{
    public PricingQuote CalculateQuote(
        VehicleType vehicleType,
        string rentalMode,
        DateTime startDate,
        DateTime endDate,
        decimal? estimatedDistance)
    {
        var quotedDays = Math.Max(1, (int)Math.Ceiling((endDate - startDate).TotalDays));
        var distance = estimatedDistance ?? 0;

        var driverFeePerDay = vehicleType.DriverFeePerDay ?? 0;
        var selfDrivePricePerDay = vehicleType.SelfDrivePricePerDay ?? vehicleType.PricePerDay;
        var includedKmPerDay = vehicleType.SelfDriveIncludedKmPerDay ?? 0;
        var extraKmPrice = vehicleType.SelfDriveExtraKmPrice ?? vehicleType.PricePerKm;

        if (rentalMode == RentalModes.SelfDrive)
        {
            var rentalAmount = selfDrivePricePerDay * quotedDays;
            var includedKm = includedKmPerDay * quotedDays;
            var extraKm = Math.Max(0, distance - includedKm);
            var distanceAmount = extraKm * extraKmPrice;
            var deposit = vehicleType.SelfDriveDepositAmount ?? 0;

            return new PricingQuote(
                selfDrivePricePerDay,
                extraKmPrice,
                quotedDays,
                distance,
                rentalAmount,
                0,
                distanceAmount,
                includedKm,
                extraKm,
                extraKmPrice,
                deposit,
                0,
                includedKmPerDay,
                extraKmPrice,
                rentalAmount + distanceAmount);
        }

        var withDriverRental = vehicleType.PricePerDay * quotedDays;
        var driverAmount = driverFeePerDay * quotedDays;
        var withDriverDistance = distance * vehicleType.PricePerKm;
        var withDriverDeposit = vehicleType.WithDriverDepositAmount ?? 0;

        return new PricingQuote(
            vehicleType.PricePerDay,
            vehicleType.PricePerKm,
            quotedDays,
            distance,
            withDriverRental,
            driverAmount,
            withDriverDistance,
            0,
            distance,
            vehicleType.PricePerKm,
            withDriverDeposit,
            driverFeePerDay,
            0,
            extraKmPrice,
            withDriverRental + driverAmount + withDriverDistance);
    }

    /// <summary>
    /// Final amount from booking snapshot + actual km. Does not read current VehicleType rates.
    /// Returns null when snapshot is insufficient. Does not modify TotalAmount.
    /// Extra km is included in this amount (3.6.2). Additive fees (late/fuel/damage) are 0
    /// until a pricing source exists.
    /// </summary>
    public decimal? CalculateFinalAmount(Booking booking, decimal? actualKm)
        => CalculateFinalBreakdown(booking, actualKm).FinalAmount;

    public PricingFinalBreakdown CalculateFinalBreakdown(
        Booking booking,
        decimal? actualKm,
        DateTime? handoverAt = null,
        DateTime? returnAt = null,
        decimal? handoverFuel = null,
        decimal? returnFuel = null,
        decimal? lateFeePerDay = null,
        decimal? fuelPrice = null)
    {
        var km = actualKm ?? booking.EstimatedDistance ?? 0;
        var lateDays = ResolveLateDays(booking.QuotedDays, handoverAt, returnAt);
        var fuelDrop = ResolveFuelDrop(handoverFuel, returnFuel);

        decimal? lateFeeAmount = lateDays > 0 && lateFeePerDay is > 0
            ? lateDays * lateFeePerDay.Value
            : null;
        decimal? fuelFeeAmount = fuelDrop > 0 && fuelPrice is > 0
            ? fuelDrop.Value * fuelPrice.Value
            : null;

        if (booking.QuotedPricePerDay is null || booking.QuotedDays is null or <= 0)
        {
            return new PricingFinalBreakdown(
                null, null, km, 0, 0, 0, lateDays, lateFeeAmount, fuelDrop, fuelFeeAmount);
        }

        var days = booking.QuotedDays.Value;
        var rentalMode = RentalModes.TryResolve(booking.RentalMode, out var resolved)
            ? resolved
            : RentalModes.WithDriver;

        decimal includedKm;
        decimal extraKm;
        decimal extraKmAmount;
        decimal baseFinal;

        if (rentalMode == RentalModes.SelfDrive)
        {
            var extraKmPrice = booking.QuotedSelfDriveExtraKmPrice ?? booking.QuotedPricePerKm;
            if (extraKmPrice is null)
            {
                return new PricingFinalBreakdown(
                    null, null, km, 0, 0, 0, lateDays, lateFeeAmount, fuelDrop, fuelFeeAmount);
            }

            var rentalAmount = booking.QuotedPricePerDay.Value * days;
            includedKm = (booking.QuotedSelfDriveIncludedKmPerDay ?? 0) * days;
            extraKm = Math.Max(0, km - includedKm);
            extraKmAmount = extraKm * extraKmPrice.Value;
            baseFinal = rentalAmount + extraKmAmount;
        }
        else
        {
            if (booking.QuotedPricePerKm is null)
            {
                return new PricingFinalBreakdown(
                    null, null, km, 0, 0, 0, lateDays, lateFeeAmount, fuelDrop, fuelFeeAmount);
            }

            var rental = booking.QuotedPricePerDay.Value * days;
            var driver = (booking.QuotedDriverFeePerDay ?? 0) * days;
            includedKm = 0;
            extraKm = km;
            extraKmAmount = km * booking.QuotedPricePerKm.Value;
            baseFinal = rental + driver + extraKmAmount;
        }

        var additive = (lateFeeAmount ?? 0) + (fuelFeeAmount ?? 0);
        return new PricingFinalBreakdown(
            baseFinal,
            baseFinal + additive,
            km,
            includedKm,
            extraKm,
            extraKmAmount,
            lateDays,
            lateFeeAmount,
            fuelDrop,
            fuelFeeAmount);
    }

    public static int ResolveLateDays(int? quotedDays, DateTime? handoverAt, DateTime? returnAt)
    {
        if (quotedDays is null or <= 0 || handoverAt is null || returnAt is null)
            return 0;

        var duration = returnAt.Value - handoverAt.Value;
        if (duration <= TimeSpan.Zero)
            return 0;

        return Math.Max(0, (int)Math.Ceiling(duration.TotalDays - quotedDays.Value));
    }

    public static decimal? ResolveFuelDrop(decimal? handoverFuel, decimal? returnFuel)
    {
        if (handoverFuel is null || returnFuel is null)
            return null;
        return handoverFuel.Value - returnFuel.Value;
    }
}

public record PricingFinalBreakdown(
    decimal? BaseFinalAmount,
    decimal? FinalAmount,
    decimal KmUsed,
    decimal IncludedKm,
    decimal ExtraKm,
    decimal ExtraKmAmount,
    int LateDays,
    decimal? LateFeeAmount,
    decimal? FuelDrop,
    decimal? FuelFeeAmount);
