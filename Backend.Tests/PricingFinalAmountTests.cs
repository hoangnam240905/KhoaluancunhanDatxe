using Backend.Entities;
using Backend.Services;
using Xunit;

namespace Backend.Tests;

public class PricingFinalAmountTests
{
    private readonly PricingService _pricing = new();

    [Fact]
    public void WithDriver_uses_snapshot_and_actual_km()
    {
        var booking = SnapshotBooking(
            rentalMode: "WithDriver",
            pricePerDay: 800_000,
            pricePerKm: 8_000,
            days: 1,
            driverFee: 200_000,
            estimated: 100,
            totalAmount: 1_800_000);

        var originalTotal = booking.TotalAmount;
        var finalAmount = _pricing.CalculateFinalAmount(booking, actualKm: 175);

        Assert.Equal(1_800_000m, originalTotal);
        Assert.Equal(1_800_000m, booking.TotalAmount);
        Assert.Equal(2_400_000m, finalAmount);
    }

    [Fact]
    public void SelfDrive_charges_only_extra_km()
    {
        var booking = SnapshotBooking(
            rentalMode: "SelfDrive",
            pricePerDay: 700_000,
            pricePerKm: 9_000,
            days: 2,
            driverFee: 0,
            estimated: 80,
            totalAmount: 1_400_000,
            includedKmPerDay: 50,
            extraKmPrice: 9_000);

        var finalAmount = _pricing.CalculateFinalAmount(booking, actualKm: 120);

        Assert.Equal(1_400_000m, booking.TotalAmount);
        Assert.Equal(1_580_000m, finalAmount);
    }

    [Fact]
    public void Null_actual_km_falls_back_to_estimated_distance_not_current_km()
    {
        var booking = SnapshotBooking(
            rentalMode: "WithDriver",
            pricePerDay: 800_000,
            pricePerKm: 8_000,
            days: 1,
            driverFee: 200_000,
            estimated: 10,
            totalAmount: 1_080_000);

        var finalAmount = _pricing.CalculateFinalAmount(booking, actualKm: null);

        Assert.Equal(1_080_000m, finalAmount);
        Assert.Equal(1_080_000m, booking.TotalAmount);
    }

    [Fact]
    public void Missing_snapshot_returns_null_and_does_not_change_total()
    {
        var booking = new Booking
        {
            RentalMode = "WithDriver",
            TotalAmount = 6_600_000,
            EstimatedDistance = 100
        };

        var finalAmount = _pricing.CalculateFinalAmount(booking, actualKm: 200);

        Assert.Null(finalAmount);
        Assert.Equal(6_600_000m, booking.TotalAmount);
    }

    [Fact]
    public void Snapshot_is_used_instead_of_current_vehicle_type_rates()
    {
        var booking = SnapshotBooking(
            rentalMode: "WithDriver",
            pricePerDay: 500_000,
            pricePerKm: 5_000,
            days: 1,
            driverFee: 100_000,
            estimated: 20,
            totalAmount: 700_000);

        var type = new VehicleType
        {
            PricePerDay = 999_000,
            PricePerKm = 99_000,
            DriverFeePerDay = 999_000
        };
        _ = type;

        Assert.Equal(700_000m, _pricing.CalculateFinalAmount(booking, actualKm: 20));
        Assert.Equal(700_000m, booking.TotalAmount);
    }

    private static Booking SnapshotBooking(
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
