using Backend.Validation;
using Xunit;

namespace Backend.Tests;

public class VehicleInspectionRulesTests
{
    [Fact]
    public void Valid_handover_resolves_type()
    {
        var error = VehicleInspectionRules.Validate(
            "Handover", 1000, 50, "Good", null, out var type);
        Assert.Null(error);
        Assert.Equal(Backend.Constants.VehicleInspectionTypes.Handover, type);
    }

    [Fact]
    public void Invalid_type_is_rejected()
    {
        var error = VehicleInspectionRules.Validate(
            "Inspection", 0, 0, null, null, out var type);
        Assert.Equal(VehicleInspectionRules.InvalidType, error);
        Assert.Equal(string.Empty, type);
    }

    [Fact]
    public void Negative_odometer_is_rejected()
    {
        var error = VehicleInspectionRules.Validate(
            "Return", -1, 40, null, null, out _);
        Assert.Equal(VehicleInspectionRules.NegativeOdometer, error);
    }

    [Fact]
    public void Fuel_outside_0_100_is_rejected()
    {
        Assert.Equal(
            VehicleInspectionRules.InvalidFuel,
            VehicleInspectionRules.Validate("Handover", 10, -1, null, null, out _));
        Assert.Equal(
            VehicleInspectionRules.InvalidFuel,
            VehicleInspectionRules.Validate("Handover", 10, 101, null, null, out _));
    }

    [Fact]
    public void Null_odometer_and_fuel_are_rejected()
    {
        Assert.Equal(
            VehicleInspectionRules.OdometerRequired,
            VehicleInspectionRules.Validate("Return", null, 50, null, null, out _));
        Assert.Equal(
            VehicleInspectionRules.FuelRequired,
            VehicleInspectionRules.Validate("Handover", 1000, null, null, null, out _));
    }

    [Fact]
    public void Return_odometer_below_current_km_is_rejected()
    {
        Assert.Equal(
            VehicleInspectionRules.OdometerBelowCurrent,
            VehicleInspectionRules.ValidateReturnOdometer(90, 100));
        Assert.Null(VehicleInspectionRules.ValidateReturnOdometer(100, 100));
        Assert.Equal(
            VehicleInspectionRules.OdometerRequired,
            VehicleInspectionRules.ValidateReturnOdometer(null, 100));
    }

    [Fact]
    public void Return_odometer_below_handover_is_rejected()
    {
        Assert.Equal(
            VehicleInspectionRules.OdometerBelowHandover,
            VehicleInspectionRules.ValidateReturnVsHandover(80, 100));
        Assert.Null(VehicleInspectionRules.ValidateReturnVsHandover(120, 100));
        Assert.Null(VehicleInspectionRules.ValidateReturnVsHandover(null, 100));
    }

    [Fact]
    public void Actual_km_requires_both_odometers()
    {
        Assert.Null(VehicleInspectionRules.ResolveActualKm(null, 200));
        Assert.Null(VehicleInspectionRules.ResolveActualKm(100, null));
        Assert.Equal(50, VehicleInspectionRules.ResolveActualKm(100, 150));
    }
}
