using System.Text.Json;
using Backend.Constants;
using Backend.DTOs.Payments;
using Backend.DTOs.Vehicles;
using Backend.Entities;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class AdminPricingPaymentTests
{
    private static UpdateVehicleTypePricingRequest ValidPricing(
        decimal pricePerDay = 900_000,
        decimal pricePerKm = 13_000) => new(
        pricePerDay, pricePerKm, 450_000, 900_000, 200, 13_000, 900_000, 4_500_000);

    [Fact]
    public async Task A_Put_valid_pricing_updates_vehicle_type_only()
    {
        using var iso = new IsolatedCarRentalDb();
        var service = new VehicleService(iso.Db);
        var (data, error, status) = await service.UpdatePricingAsync(1, ValidPricing());

        Assert.Null(error);
        Assert.Equal(200, status);
        Assert.NotNull(data);
        Assert.Equal(900_000m, data!.PricePerDay);
        Assert.Equal(13_000m, data.PricePerKm);
        Assert.Equal(450_000m, data.DriverFeePerDay);
        Assert.Equal(900_000m, data.SelfDrivePricePerDay);
        Assert.Equal(200m, data.SelfDriveIncludedKmPerDay);
        Assert.Equal(13_000m, data.SelfDriveExtraKmPrice);
        Assert.Equal(900_000m, data.WithDriverDepositAmount);
        Assert.Equal(4_500_000m, data.SelfDriveDepositAmount);
        Assert.Equal("4 chỗ - Sedan", data.TypeName);

        var stored = await iso.Db.VehicleTypes.AsNoTracking().SingleAsync(t => t.TypeId == 1);
        Assert.Equal(900_000m, stored.PricePerDay);
        Assert.Equal(13_000m, stored.PricePerKm);
    }

    [Fact]
    public async Task B_Put_negative_price_returns_400()
    {
        using var iso = new IsolatedCarRentalDb();
        var service = new VehicleService(iso.Db);
        var (data, error, status) = await service.UpdatePricingAsync(1, ValidPricing(pricePerDay: -1));

        Assert.Null(data);
        Assert.Equal(400, status);
        Assert.Contains("âm", error, StringComparison.OrdinalIgnoreCase);
        var stored = await iso.Db.VehicleTypes.AsNoTracking().SingleAsync(t => t.TypeId == 1);
        Assert.Equal(800_000m, stored.PricePerDay);
    }

    [Fact]
    public async Task C_Put_null_price_returns_400()
    {
        using var iso = new IsolatedCarRentalDb();
        var service = new VehicleService(iso.Db);
        var request = new UpdateVehicleTypePricingRequest(
            null, 13_000, 450_000, 900_000, 200, 13_000, 900_000, 4_500_000);
        var (data, error, status) = await service.UpdatePricingAsync(1, request);

        Assert.Null(data);
        Assert.Equal(400, status);
        Assert.Contains("8 giá", error);
        var stored = await iso.Db.VehicleTypes.AsNoTracking().SingleAsync(t => t.TypeId == 1);
        Assert.Equal(800_000m, stored.PricePerDay);
    }

    [Fact]
    public async Task D_Put_does_not_change_bookings()
    {
        using var iso = new IsolatedCarRentalDb();
        var before = await iso.Db.Bookings.AsNoTracking().OrderBy(b => b.BookingId).ToListAsync();
        var service = new VehicleService(iso.Db);
        await service.UpdatePricingAsync(1, ValidPricing());
        await service.UpdatePricingAsync(2, ValidPricing(1_300_000, 16_000));

        var after = await iso.Db.Bookings.AsNoTracking().OrderBy(b => b.BookingId).ToListAsync();
        Assert.Equal(before.Count, after.Count);
        for (var i = 0; i < before.Count; i++)
        {
            Assert.Equal(before[i].BookingId, after[i].BookingId);
            Assert.Equal(before[i].TotalAmount, after[i].TotalAmount);
            Assert.Equal(before[i].FinalAmount, after[i].FinalAmount);
            Assert.Equal(before[i].QuotedPricePerDay, after[i].QuotedPricePerDay);
            Assert.Equal(before[i].QuotedPricePerKm, after[i].QuotedPricePerKm);
            Assert.Equal(before[i].QuotedDays, after[i].QuotedDays);
            Assert.Equal(before[i].QuotedDriverFeePerDay, after[i].QuotedDriverFeePerDay);
            Assert.Equal(before[i].QuotedDepositAmount, after[i].QuotedDepositAmount);
            Assert.Equal(before[i].Status, after[i].Status);
            Assert.Equal(before[i].VehicleTypeId, after[i].VehicleTypeId);
        }
    }

    [Fact]
    public async Task E_Booking_1_invariants_survive_pricing_put()
    {
        using var iso = new IsolatedCarRentalDb();
        await new VehicleService(iso.Db).UpdatePricingAsync(2, ValidPricing(1_500_000, 20_000));

        var b1 = await iso.Db.Bookings.AsNoTracking()
            .Include(b => b.TripAssignment)
            .SingleAsync(b => b.BookingId == 1);
        Assert.Equal(6_600_000m, b1.TotalAmount);
        Assert.Null(b1.FinalAmount);
        Assert.Equal(BookingStatuses.Assigned, b1.Status);
        Assert.Null(b1.QuotedPricePerDay);
        Assert.Null(b1.QuotedPricePerKm);
        Assert.Null(b1.QuotedDays);
        Assert.Null(b1.QuotedDepositAmount);
        Assert.NotNull(b1.TripAssignment);
        Assert.Equal(5, b1.TripAssignment!.DriverId);
        Assert.Equal(3, b1.TripAssignment.VehicleId);
        Assert.Equal(3, b1.AssignedVehicleId);
    }

    [Fact]
    public async Task F_Booking_2_invariants_survive_pricing_put()
    {
        using var iso = new IsolatedCarRentalDb();
        await new VehicleService(iso.Db).UpdatePricingAsync(1, ValidPricing());

        var b2 = await iso.Db.Bookings.AsNoTracking()
            .Include(b => b.TripAssignment)
            .SingleAsync(b => b.BookingId == 2);
        Assert.Equal(2_240_000m, b2.TotalAmount);
        Assert.Null(b2.FinalAmount);
        Assert.NotNull(b2.TripAssignment);
        Assert.Equal(5, b2.TripAssignment!.DriverId);
        Assert.Equal(1, b2.TripAssignment.VehicleId);
    }

    [Fact]
    public async Task G_Payment_1_legacy_row_unchanged_after_pricing_put()
    {
        using var iso = new IsolatedCarRentalDb();
        await new VehicleService(iso.Db).UpdatePricingAsync(1, ValidPricing());

        var p1 = await iso.Db.Payments.AsNoTracking().SingleAsync(p => p.PaymentId == 1);
        Assert.Equal(2, p1.BookingId);
        Assert.Equal(2_240_000m, p1.Amount);
        Assert.Equal("BankTransfer", p1.Method);
        Assert.Equal("Paid", p1.Status);
        Assert.Equal("TXN-20260825-001", p1.TransactionRef);
        Assert.Null(p1.PaymentType);
    }

    [Fact]
    public async Task H_Admin_get_payments_serializes_null_payment_type()
    {
        using var iso = new IsolatedCarRentalDb();
        var (rows, error, status) = await new PaymentService(iso.Db, new ScheduleConflictService(iso.Db)).GetAdminAsync(null, null, null);
        Assert.Null(error);
        Assert.Equal(200, status);
        Assert.NotNull(rows);
        var p1 = rows!.Single(p => p.PaymentId == 1);
        Assert.Null(p1.PaymentType);

        var json = JsonSerializer.Serialize(p1, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"paymentType\":null", json.Replace(" ", "", StringComparison.Ordinal));
    }

    [Fact]
    public async Task H_Admin_get_payments_unknown_booking_is_404()
    {
        using var iso = new IsolatedCarRentalDb();
        var (rows, error, status) = await new PaymentService(iso.Db, new ScheduleConflictService(iso.Db)).GetAdminAsync(99999, null, null);
        Assert.Null(rows);
        Assert.Equal(404, status);
        Assert.Contains("Không tìm thấy đơn", error);
    }

    [Fact]
    public async Task I_Customer_create_deposit_still_pending()
    {
        using var iso = new IsolatedCarRentalDb();
        var booking = iso.AddDepositBooking(800_000);
        var (payment, error, status) = await new PaymentService(iso.Db, new ScheduleConflictService(iso.Db)).CreateAsync(
            3, new CreatePaymentRequest(booking.BookingId, PaymentTypes.Deposit, PaymentMethods.Cash));

        Assert.Null(error);
        Assert.Equal(201, status);
        Assert.NotNull(payment);
        Assert.Equal(PaymentTypes.Deposit, payment!.PaymentType);
        Assert.Equal(800_000m, payment.Amount);
        Assert.Equal(PaymentStatuses.Pending, payment.Status);
        Assert.Null(payment.PaidAt);
        Assert.Equal(PaymentMethods.Cash, payment.Method);
    }

    [Fact]
    public void J_Public_vehicle_type_dto_has_old_contract_only()
    {
        var names = typeof(VehicleTypeResponse).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Contains("TypeId", names);
        Assert.Contains("TypeName", names);
        Assert.Contains("SeatCapacity", names);
        Assert.Contains("PricePerDay", names);
        Assert.Contains("PricePerKm", names);
        Assert.Contains("Description", names);
        Assert.Contains("ImageUrl", names);
        Assert.DoesNotContain("DriverFeePerDay", names);
        Assert.DoesNotContain("SelfDrivePricePerDay", names);
        Assert.DoesNotContain("SelfDriveIncludedKmPerDay", names);
        Assert.DoesNotContain("SelfDriveExtraKmPrice", names);
        Assert.DoesNotContain("WithDriverDepositAmount", names);
        Assert.DoesNotContain("SelfDriveDepositAmount", names);
        Assert.Equal(7, names.Count);
    }

    [Fact]
    public async Task Admin_get_vehicle_types_includes_eight_pricing_fields()
    {
        using var iso = new IsolatedCarRentalDb();
        var list = await new VehicleService(iso.Db).GetAdminVehicleTypesAsync();
        Assert.Equal(4, list.Count);
        var first = list[0];
        Assert.True(first.PricePerDay >= 0);
        Assert.True(first.DriverFeePerDay >= 0);
        Assert.True(first.SelfDriveDepositAmount >= 0);
        var byId = await new VehicleService(iso.Db).GetAdminVehicleTypeAsync(1);
        Assert.NotNull(byId);
        Assert.Equal(first.TypeName, byId!.TypeName);
    }
}
