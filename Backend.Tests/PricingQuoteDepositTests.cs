using Backend.Constants;
using Backend.Entities;
using Backend.Services;
using Backend.Validation;
using Xunit;

namespace Backend.Tests;

public class PricingQuoteDepositTests
{
    private static readonly DateTime Start = new(2026, 12, 10, 8, 0, 0);
    private readonly PricingService _pricing = new();

    private static VehicleType Type(
        decimal day,
        decimal? selfDriveDeposit = null,
        decimal? withDriverDeposit = null)
        => new()
        {
            TypeId = 1,
            TypeName = "Sedan",
            PricePerDay = day,
            PricePerKm = 10_000,
            DriverFeePerDay = 0,
            SelfDrivePricePerDay = day,
            SelfDriveIncludedKmPerDay = 200,
            SelfDriveExtraKmPrice = 10_000,
            WithDriverDepositAmount = withDriverDeposit ?? day,
            SelfDriveDepositAmount = selfDriveDeposit ?? day * 5
        };

    [Fact]
    public void SelfDrive_one_day_deposit_is_half_total_not_catalog_5x()
    {
        var start = Start;
        var end = start.AddDays(1);
        var quote = _pricing.CalculateQuote(Type(2_500_000), RentalModes.SelfDrive, start, end, 0);
        Assert.Equal(1, quote.QuotedDays);
        Assert.Equal(2_500_000m, quote.TotalAmount);
        Assert.Equal(1_250_000m, quote.DepositAmount);
        Assert.Equal(PricingDefaults.DepositFromTotal(quote.TotalAmount), quote.DepositAmount);
    }

    [Fact]
    public void SelfDrive_two_days_recalculates_total_and_half_deposit()
    {
        var start = Start;
        var oneDay = _pricing.CalculateQuote(Type(2_500_000), RentalModes.SelfDrive, start, start.AddDays(1), 0);
        var twoDays = _pricing.CalculateQuote(Type(2_500_000), RentalModes.SelfDrive, start, start.AddDays(2), 0);
        Assert.Equal(1, oneDay.QuotedDays);
        Assert.Equal(2, twoDays.QuotedDays);
        Assert.Equal(2_500_000m, oneDay.TotalAmount);
        Assert.Equal(5_000_000m, twoDays.TotalAmount);
        Assert.Equal(1_250_000m, oneDay.DepositAmount);
        Assert.Equal(2_500_000m, twoDays.DepositAmount);
    }

    [Fact]
    public void SelfDrive_three_days_keeps_50_percent_deposit()
    {
        var start = Start;
        var quote = _pricing.CalculateQuote(Type(1_000_000), RentalModes.SelfDrive, start, start.AddDays(3), 0);
        Assert.Equal(3, quote.QuotedDays);
        Assert.Equal(3_000_000m, quote.TotalAmount);
        Assert.Equal(1_500_000m, quote.DepositAmount);
    }

    [Fact]
    public void Catalog_self_drive_deposit_column_does_not_override_quote()
    {
        var start = Start;
        var quote = _pricing.CalculateQuote(
            Type(2_500_000, selfDriveDeposit: 12_500_000),
            RentalModes.SelfDrive,
            start,
            start.AddDays(1),
            0);
        Assert.Equal(1_250_000m, quote.DepositAmount);
        Assert.NotEqual(12_500_000m, quote.DepositAmount);
    }

    [Fact]
    public void WithDriver_deposit_is_half_of_total_including_driver_and_km()
    {
        var vt = Type(1_000_000, withDriverDeposit: 1_000_000);
        vt.DriverFeePerDay = 500_000;
        var start = Start;
        var quote = _pricing.CalculateQuote(vt, RentalModes.WithDriver, start, start.AddDays(1), 100);
        // 1_000_000 rental + 500_000 driver + 100*10_000 km = 2_500_000
        Assert.Equal(2_500_000m, quote.TotalAmount);
        Assert.Equal(1_250_000m, quote.DepositAmount);
    }

    [Fact]
    public void Same_calendar_day_later_time_is_one_rental_day()
    {
        var start = Start;
        var end = start.AddHours(8);
        Assert.Equal(1, BookingDateRules.QuotedDays(start, end));
        var quote = _pricing.CalculateQuote(Type(800_000), RentalModes.SelfDrive, start, end, 0);
        Assert.Equal(1, quote.QuotedDays);
        Assert.Equal(800_000m, quote.TotalAmount);
        Assert.Equal(400_000m, quote.DepositAmount);
    }
}

public class BookingDateRulesTests
{
    [Fact]
    public void End_equal_or_before_start_is_rejected()
    {
        var start = VietnamTime.Now.Date.AddDays(2).AddHours(8);
        Assert.Equal(BookingDateRules.EndMustBeAfterStart, BookingDateRules.ValidateNewRental(start, start));
        Assert.Equal(BookingDateRules.EndMustBeAfterStart, BookingDateRules.ValidateNewRental(start, start.AddHours(-1)));
    }

    [Fact]
    public void Start_yesterday_is_rejected()
    {
        var start = VietnamTime.Today.AddDays(-1).ToDateTime(new TimeOnly(8, 0));
        var end = start.AddDays(1);
        Assert.Equal(BookingDateRules.StartCannotBePast, BookingDateRules.ValidateNewRental(start, end));
    }

    [Fact]
    public void Start_today_with_later_end_is_allowed()
    {
        var start = VietnamTime.Today.ToDateTime(TimeOnly.MinValue);
        var end = start.AddHours(8);
        Assert.Null(BookingDateRules.ValidateNewRental(start, end));
    }

    [Fact]
    public void Valid_multi_day_range_passes()
    {
        var start = VietnamTime.Today.AddDays(1).ToDateTime(new TimeOnly(8, 0));
        var end = start.AddDays(2);
        Assert.Null(BookingDateRules.ValidateNewRental(start, end));
        Assert.Equal(2, BookingDateRules.QuotedDays(start, end));
    }
}
