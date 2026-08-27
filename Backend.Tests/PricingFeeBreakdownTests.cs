using Backend.Constants;
using Backend.Entities;
using Backend.Services;
using Backend.Validation;
using Xunit;

namespace Backend.Tests;

public class PricingFeeBreakdownTests
{
    private readonly PricingService _pricing = new();

    [Fact]
    public void WithDriver_actual_equals_estimated_extra_km_is_in_base_not_added_again()
    {
        var booking = Snapshot("WithDriver", 800_000, 8_000, 1, 200_000, 100, 1_800_000);
        var b = _pricing.CalculateFinalBreakdown(booking, actualKm: 100);

        Assert.Equal(1_800_000m, booking.TotalAmount);
        Assert.Equal(100m, b.ExtraKm);
        Assert.Equal(800_000m, b.ExtraKmAmount);
        Assert.Equal(1_800_000m, b.BaseFinalAmount);
        Assert.Equal(1_800_000m, b.FinalAmount);
        Assert.Equal(b.BaseFinalAmount, b.FinalAmount);
        Assert.NotEqual(b.BaseFinalAmount + b.ExtraKmAmount, b.FinalAmount);
    }

    [Fact]
    public void WithDriver_actual_above_estimated_uses_snapshot_km_rate()
    {
        var booking = Snapshot("WithDriver", 800_000, 8_000, 1, 200_000, 100, 1_800_000);
        var b = _pricing.CalculateFinalBreakdown(booking, actualKm: 175);

        Assert.Equal(1_800_000m, booking.TotalAmount);
        Assert.Equal(175m, b.ExtraKm);
        Assert.Equal(1_400_000m, b.ExtraKmAmount);
        Assert.Equal(2_400_000m, b.BaseFinalAmount);
        Assert.Equal(2_400_000m, b.FinalAmount);
        Assert.NotEqual(b.BaseFinalAmount + b.ExtraKmAmount, b.FinalAmount);
    }

    [Fact]
    public void SelfDrive_under_included_has_zero_extra_km()
    {
        var booking = Snapshot("SelfDrive", 700_000, 9_000, 2, 0, 80, 1_400_000, 50, 9_000);
        var b = _pricing.CalculateFinalBreakdown(booking, actualKm: 90);

        Assert.Equal(1_400_000m, booking.TotalAmount);
        Assert.Equal(0m, b.ExtraKm);
        Assert.Equal(0m, b.ExtraKmAmount);
        Assert.Equal(1_400_000m, b.FinalAmount);
    }

    [Fact]
    public void SelfDrive_over_included_charges_only_extra_km_once()
    {
        var booking = Snapshot("SelfDrive", 700_000, 9_000, 2, 0, 80, 1_400_000, 50, 9_000);
        var b = _pricing.CalculateFinalBreakdown(booking, actualKm: 120);

        Assert.Equal(1_400_000m, booking.TotalAmount);
        Assert.Equal(20m, b.ExtraKm);
        Assert.Equal(180_000m, b.ExtraKmAmount);
        Assert.Equal(1_580_000m, b.BaseFinalAmount);
        Assert.Equal(1_580_000m, b.FinalAmount);
        Assert.NotEqual(b.BaseFinalAmount + b.ExtraKmAmount, b.FinalAmount);
    }

    [Fact]
    public void Missing_odometer_falls_back_to_estimated_distance()
    {
        var booking = Snapshot("WithDriver", 800_000, 8_000, 1, 200_000, 10, 1_080_000);
        var b = _pricing.CalculateFinalBreakdown(booking, actualKm: null);

        Assert.Equal(10m, b.KmUsed);
        Assert.Equal(1_080_000m, b.FinalAmount);
        Assert.Equal(1_080_000m, booking.TotalAmount);
    }

    [Fact]
    public void Fuel_drop_without_fuel_price_does_not_invent_fee()
    {
        var booking = Snapshot("SelfDrive", 700_000, 9_000, 1, 0, 0, 700_000, 200, 9_000);
        var b = _pricing.CalculateFinalBreakdown(
            booking, actualKm: 10, handoverFuel: 80, returnFuel: 40);

        Assert.Equal(40m, b.FuelDrop);
        Assert.Null(b.FuelFeeAmount);
        Assert.Equal(b.BaseFinalAmount, b.FinalAmount);
    }

    [Fact]
    public void On_time_return_has_no_late_days()
    {
        var start = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
        var booking = Snapshot("SelfDrive", 700_000, 9_000, 1, 0, 0, 700_000, 200, 9_000);
        var b = _pricing.CalculateFinalBreakdown(
            booking, 0, handoverAt: start, returnAt: start.AddHours(20));

        Assert.Equal(0, b.LateDays);
        Assert.Null(b.LateFeeAmount);
    }

    [Fact]
    public void Late_return_without_late_fee_per_day_does_not_invent_amount()
    {
        var start = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
        var booking = Snapshot("SelfDrive", 700_000, 9_000, 1, 0, 0, 700_000, 200, 9_000);
        var b = _pricing.CalculateFinalBreakdown(
            booking, 0, handoverAt: start, returnAt: start.AddDays(2.2));

        Assert.Equal(2, b.LateDays);
        Assert.Null(b.LateFeeAmount);
        Assert.Equal(700_000m, b.FinalAmount);
        Assert.Equal(700_000m, booking.TotalAmount);
    }

    [Fact]
    public void Missing_handover_cannot_compute_late_days()
    {
        var booking = Snapshot("WithDriver", 800_000, 8_000, 1, 200_000, 10, 1_080_000);
        var b = _pricing.CalculateFinalBreakdown(
            booking, 10, handoverAt: null, returnAt: DateTime.UtcNow);

        Assert.Equal(0, b.LateDays);
        Assert.Null(b.LateFeeAmount);
    }

    [Fact]
    public void Old_booking_without_snapshot_stays_null_and_does_not_change_total()
    {
        var booking = new Booking { RentalMode = "WithDriver", TotalAmount = 6_600_000, EstimatedDistance = 100 };
        var b = _pricing.CalculateFinalBreakdown(booking, 200);

        Assert.Null(b.BaseFinalAmount);
        Assert.Null(b.FinalAmount);
        Assert.Equal(6_600_000m, booking.TotalAmount);
    }

    [Fact]
    public void Late_fee_rate_when_provided_is_additive_not_replacing_extra_km()
    {
        var start = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
        var booking = Snapshot("WithDriver", 800_000, 8_000, 1, 200_000, 100, 1_800_000);
        var b = _pricing.CalculateFinalBreakdown(
            booking, 100, start, start.AddDays(2), lateFeePerDay: 50_000);

        Assert.Equal(1, b.LateDays);
        Assert.Equal(50_000m, b.LateFeeAmount);
        Assert.Equal(1_800_000m, b.BaseFinalAmount);
        Assert.Equal(1_850_000m, b.FinalAmount);
        Assert.Equal(800_000m, b.ExtraKmAmount);
        Assert.NotEqual(b.BaseFinalAmount + b.ExtraKmAmount + b.LateFeeAmount, b.FinalAmount);
    }

    [Fact]
    public void Negative_fee_amount_is_rejected()
    {
        Assert.Equal(
            BookingFeeRules.NegativeAmount,
            BookingFeeRules.Validate(BookingFeeTypes.ExtraKm, -1, null, out _));
    }

    [Fact]
    public void Invalid_fee_type_is_rejected()
    {
        Assert.Equal(
            BookingFeeRules.InvalidType,
            BookingFeeRules.Validate("Discount", 1, null, out _));
    }

    private static Booking Snapshot(
        string rentalMode,
        decimal pricePerDay,
        decimal pricePerKm,
        int days,
        decimal driverFee,
        decimal estimated,
        decimal totalAmount,
        decimal? includedKmPerDay = null,
        decimal? extraKmPrice = null)
        => new()
        {
            RentalMode = rentalMode,
            QuotedPricePerDay = pricePerDay,
            QuotedPricePerKm = pricePerKm,
            QuotedDays = days,
            QuotedDriverFeePerDay = driverFee,
            QuotedSelfDriveIncludedKmPerDay = includedKmPerDay,
            QuotedSelfDriveExtraKmPrice = extraKmPrice,
            EstimatedDistance = estimated,
            TotalAmount = totalAmount
        };
}
