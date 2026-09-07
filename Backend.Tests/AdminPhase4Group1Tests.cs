using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.DTOs.Customers;
using Backend.DTOs.Drivers;
using Backend.DTOs.Vehicles;
using Backend.Entities;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Backend.Tests;

public class AdminPhase4Group1Tests
{
    private static AuthService Auth(IsolatedCarRentalDb iso)
    {
        var config = new ConfigurationManager();
        config["Jwt:Key"] = "CarRentalSystem_SuperSecretKey_2026_DoAnCuNhan!";
        config["Jwt:Issuer"] = "CarRentalAPI";
        config["Jwt:Audience"] = "CarRentalClients";
        config["Jwt:ExpireHours"] = "8";
        return new AuthService(iso.Db, new JwtTokenService(config));
    }

    private static DriverService Drivers(IsolatedCarRentalDb iso)
    {
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        return new DriverService(iso.Db, bookings, inspections, fees);
    }

    [Fact]
    public async Task Change_password_rejects_wrong_old_password()
    {
        using var iso = new IsolatedCarRentalDb();
        var (ok, error) = await Auth(iso).ChangePasswordAsync(
            3, new ChangePasswordRequest("WrongPass1!", "Password456!"));

        Assert.False(ok);
        Assert.Equal("Mật khẩu cũ không đúng.", error);
        Assert.True(BCrypt.Net.BCrypt.Verify("Password123!", iso.Db.Users.Find(3)!.PasswordHash));
    }

    [Fact]
    public async Task Change_password_updates_hash_and_login_works()
    {
        using var iso = new IsolatedCarRentalDb();
        var auth = Auth(iso);
        var (ok, error) = await auth.ChangePasswordAsync(
            3, new ChangePasswordRequest("Password123!", "Password456!"));

        Assert.True(ok);
        Assert.Null(error);
        Assert.True(BCrypt.Net.BCrypt.Verify("Password456!", iso.Db.Users.Find(3)!.PasswordHash));

        var (login, loginError, status) = await auth.LoginAsync(
            new LoginRequest("customer1@gmail.com", "Password456!"));
        Assert.Equal(200, status);
        Assert.Null(loginError);
        Assert.NotNull(login);
    }

    [Fact]
    public async Task Locked_customer_login_is_403_after_password_ok()
    {
        using var iso = new IsolatedCarRentalDb();
        var customers = new AdminCustomerService(iso.Db);
        var (locked, lockError, lockStatus) = await customers.SetLockedAsync(3, true);
        Assert.Equal(200, lockStatus);
        Assert.Null(lockError);
        Assert.True(locked!.IsLocked);

        var auth = Auth(iso);
        var (data, error, status) = await auth.LoginAsync(
            new LoginRequest("customer1@gmail.com", "Password123!"));
        Assert.Null(data);
        Assert.Equal(403, status);
        Assert.Equal("Tài khoản đã bị khóa.", error);
    }

    [Fact]
    public async Task Locked_customer_wrong_password_is_still_401()
    {
        using var iso = new IsolatedCarRentalDb();
        await new AdminCustomerService(iso.Db).SetLockedAsync(3, true);
        var (data, error, status) = await Auth(iso).LoginAsync(
            new LoginRequest("customer1@gmail.com", "WrongPass1!"));
        Assert.Null(data);
        Assert.Equal(401, status);
        Assert.Equal("Email hoặc mật khẩu không đúng.", error);
    }

    [Fact]
    public async Task Create_vehicle_type_fills_pricing_defaults_immediately()
    {
        using var iso = new IsolatedCarRentalDb();
        var (data, error, status) = await new VehicleService(iso.Db).CreateTypeAsync(
            new CreateVehicleTypeRequest("Xe thử nghiệm", 1_000_000, 15_000));

        Assert.Null(error);
        Assert.Equal(201, status);
        Assert.NotNull(data);
        Assert.Equal("Xe thử nghiệm", data!.TypeName);
        Assert.Equal(1_000_000m, data.PricePerDay);
        Assert.Equal(15_000m, data.PricePerKm);
        Assert.Equal(500_000m, data.DriverFeePerDay);
        Assert.Equal(1_000_000m, data.SelfDrivePricePerDay);
        Assert.Equal(200m, data.SelfDriveIncludedKmPerDay);
        Assert.Equal(15_000m, data.SelfDriveExtraKmPrice);
        Assert.Equal(1_000_000m, data.WithDriverDepositAmount);
        Assert.Equal(5_000_000m, data.SelfDriveDepositAmount);
        Assert.True(data.IsActive);
        Assert.Equal(4, data.SeatCapacity);

        var stored = await iso.Db.VehicleTypes.AsNoTracking().SingleAsync(t => t.TypeId == data.TypeId);
        Assert.Equal(500_000m, stored.DriverFeePerDay);
        Assert.Equal(5_000_000m, stored.SelfDriveDepositAmount);
        Assert.False(string.IsNullOrWhiteSpace(stored.TypeName));
    }

    [Fact]
    public async Task Delete_vehicle_type_with_vehicles_is_400()
    {
        using var iso = new IsolatedCarRentalDb();
        var (ok, error, status) = await new VehicleService(iso.Db).DeleteTypeAsync(1);
        Assert.False(ok);
        Assert.Equal(400, status);
        Assert.Equal("Không thể xóa loại xe đang có phương tiện.", error);
        Assert.True(await iso.Db.VehicleTypes.AnyAsync(t => t.TypeId == 1));
    }

    [Fact]
    public async Task Delete_empty_vehicle_type_succeeds()
    {
        using var iso = new IsolatedCarRentalDb();
        var created = await new VehicleService(iso.Db).CreateTypeAsync(
            new CreateVehicleTypeRequest("Loại trống", 800_000, 12_000));
        var id = created.Data!.TypeId;
        var (ok, error, status) = await new VehicleService(iso.Db).DeleteTypeAsync(id);
        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(200, status);
        Assert.False(await iso.Db.VehicleTypes.AnyAsync(t => t.TypeId == id));
    }

    [Fact]
    public async Task Soft_delete_driver_with_open_assignment_is_blocked()
    {
        using var iso = new IsolatedCarRentalDb();
        var before = await iso.Db.TripAssignments.AsNoTracking().ToListAsync();
        var (ok, error, status) = await Drivers(iso).SoftDeleteAsync(5);

        Assert.False(ok);
        Assert.Equal(400, status);
        Assert.Equal("Không thể xóa tài xế đang có chuyến chưa hoàn thành.", error);
        Assert.True(iso.Db.Drivers.Find(5)!.IsActive);
        Assert.True(await iso.Db.Users.AnyAsync(u => u.UserId == 5));
        var after = await iso.Db.TripAssignments.AsNoTracking().ToListAsync();
        Assert.Equal(before.Count, after.Count);
        Assert.Contains(after, t => t.DriverId == 5 && t.Status == TripAssignmentStatuses.Assigned);
    }

    [Fact]
    public async Task Soft_delete_driver_hides_from_available_list_and_keeps_history()
    {
        using var iso = new IsolatedCarRentalDb();
        var drivers = Drivers(iso);
        var (created, createError, createStatus) = await drivers.CreateAdminDriverAsync(
            new CreateAdminDriverRequest("Tai Xe Moi", "newdriver@carrental.vn", "0923999001", "Password123!"));
        Assert.Null(createError);
        Assert.Equal(201, createStatus);
        var driverId = created!.DriverId;

        var historyBooking = new Booking
        {
            CustomerId = 3,
            VehicleTypeId = 1,
            PickupAddress = "A",
            DropoffAddress = "B",
            StartDate = new DateTime(2026, 7, 1, 8, 0, 0),
            EndDate = new DateTime(2026, 7, 2, 18, 0, 0),
            EstimatedDistance = 40,
            TotalAmount = 800_000,
            Status = BookingStatuses.Completed,
            RentalMode = RentalModes.WithDriver,
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        };
        iso.Db.Bookings.Add(historyBooking);
        await iso.Db.SaveChangesAsync();

        iso.Db.TripAssignments.Add(new TripAssignment
        {
            BookingId = historyBooking.BookingId,
            DriverId = driverId,
            VehicleId = 2,
            AssignedBy = 2,
            AssignedAt = DateTime.UtcNow.AddDays(-10),
            Status = TripAssignmentStatuses.Completed
        });
        await iso.Db.SaveChangesAsync();

        var (ok, error, status) = await drivers.SoftDeleteAsync(driverId);
        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(200, status);

        var row = await iso.Db.Drivers.Include(d => d.User).SingleAsync(d => d.DriverId == driverId);
        Assert.False(row.IsActive);
        Assert.True(await iso.Db.Users.AnyAsync(u => u.UserId == driverId));
        Assert.Equal("newdriver@carrental.vn", row.User.Email);

        var available = await drivers.GetDriversAsync();
        Assert.DoesNotContain(available, d => d.DriverId == driverId);

        var adminList = await drivers.GetAdminDriversAsync();
        Assert.Contains(adminList, d => d.DriverId == driverId && !d.IsActive);

        Assert.True(await iso.Db.TripAssignments.AnyAsync(t =>
            t.DriverId == driverId && t.Status == TripAssignmentStatuses.Completed));
        Assert.True(await iso.Db.TripAssignments.AnyAsync(t => t.DriverId == 5));
    }
}
