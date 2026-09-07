using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.DTOs.Payments;
using Backend.DTOs.Vehicles;
using Backend.Entities;
using Backend.Services;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class PhaseP0VehicleDeleteProtectionTests
{
    private static readonly DateTime Start = new(2026, 12, 22, 10, 0, 0);
    private static readonly DateTime End = new(2026, 12, 22, 14, 0, 0);

    private static CreateBookingRequest SelfDrive(int vehicleId)
        => new(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.SelfDrive, vehicleId);

    private static CreateBookingRequest WithDriver()
        => new(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.WithDriver);

    private static CreatePaymentRequest Deposit(int bookingId)
        => new(bookingId, PaymentTypes.Deposit, PaymentMethods.Cash);

    [Fact]
    public async Task Unused_vehicle_can_be_deleted()
    {
        using var iso = new IsolatedCarRentalDb();
        var vehicleId = AddSpare(iso);
        var (ok, error, status) = await Vehicles(iso.Db).DeleteVehicleAsync(vehicleId);
        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(204, status);
        Assert.Null(iso.Db.Vehicles.Find(vehicleId));
    }

    [Fact]
    public async Task Open_trip_assignment_blocks_delete()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, payments, dispatch, drivers, vehicles, schedule) = Services(iso);
        var vehicleId = AddSpare(iso);
        iso.SetLastCompletedMaintenance(vehicleId, DateTime.UtcNow.AddDays(-10), 1000);
        var created = await bookings.CreateBookingAsync(3, WithDriver());
        await dispatch.ConfirmBookingAsync(created!.BookingId, 2);
        var assigned = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(6, vehicleId), 2);
        Assert.Null(assigned.Error);
        Assert.True(await schedule.IsVehicleOccupiedAsync(vehicleId));

        var (ok, error, status) = await vehicles.DeleteVehicleAsync(vehicleId);
        Assert.False(ok);
        Assert.Equal(400, status);
        Assert.Equal(VehicleOccupancyRules.CannotDelete, error);
        Assert.NotNull(iso.Db.Vehicles.Find(vehicleId));

        var assignmentId = iso.Db.TripAssignments.Single(t => t.BookingId == created.BookingId).AssignmentId;
        Assert.True(await drivers.AcceptTripAsync(6, assignmentId));
        Assert.True(await drivers.StartTripAsync(6, assignmentId));
        Assert.Equal(TripAssignmentStatuses.InProgress,
            iso.Db.TripAssignments.Find(assignmentId)!.Status);
        var again = await vehicles.DeleteVehicleAsync(vehicleId);
        Assert.False(again.Ok);
        Assert.Equal(VehicleOccupancyRules.CannotDelete, again.Error);
    }

    [Fact]
    public async Task Pending_hold_with_deposit_pending_or_paid_blocks_delete()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, payments, _, _, vehicles, schedule) = Services(iso);
        var pendingId = AddSpare(iso, "51Z-HPEND1");
        iso.SetLastCompletedMaintenance(pendingId, DateTime.UtcNow.AddDays(-10), 1000);
        var pendingHold = await bookings.CreateBookingAsync(3, SelfDrive(pendingId));
        var createdPay = await payments.CreateAsync(3, Deposit(pendingHold!.BookingId));
        Assert.Equal(201, createdPay.StatusCode);
        Assert.Equal(PaymentStatuses.Pending, createdPay.Payment!.Status);
        Assert.True(await schedule.IsVehicleOccupiedAsync(pendingId));
        var blockedPending = await vehicles.DeleteVehicleAsync(pendingId);
        Assert.False(blockedPending.Ok);
        Assert.Equal(VehicleOccupancyRules.CannotDelete, blockedPending.Error);

        var paidId = AddSpare(iso, "51Z-HPAID1");
        iso.SetLastCompletedMaintenance(paidId, DateTime.UtcNow.AddDays(-10), 1000);
        var paidHold = await bookings.CreateBookingAsync(3, SelfDrive(paidId));
        var paid = await payments.CreateAsync(3, Deposit(paidHold!.BookingId));
        await payments.SimulateSuccessAsync(3, paid.Payment!.PaymentId);
        Assert.Equal(PaymentStatuses.Paid, iso.Db.Payments.Find(paid.Payment.PaymentId)!.Status);
        Assert.True(await schedule.IsVehicleOccupiedAsync(paidId));
        var blockedPaid = await vehicles.DeleteVehicleAsync(paidId);
        Assert.False(blockedPaid.Ok);
        Assert.Equal(VehicleOccupancyRules.CannotDelete, blockedPaid.Error);
        Assert.NotNull(iso.Db.Vehicles.Find(paidId));
    }

    [Fact]
    public async Task Confirmed_and_assigned_bookings_that_hold_vehicle_block_delete()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, payments, dispatch, _, vehicles, schedule) = Services(iso);
        var vehicleId = AddSpare(iso);
        iso.SetLastCompletedMaintenance(vehicleId, DateTime.UtcNow.AddDays(-10), 1000);
        var created = await bookings.CreateBookingAsync(3, SelfDrive(vehicleId));
        await payments.CreateAsync(3, Deposit(created!.BookingId));
        await dispatch.ConfirmBookingAsync(created.BookingId, 2);
        Assert.Equal(BookingStatuses.Confirmed, iso.Db.Bookings.Find(created.BookingId)!.Status);
        Assert.True(await schedule.IsVehicleOccupiedAsync(vehicleId));
        var blockedConfirmed = await vehicles.DeleteVehicleAsync(vehicleId);
        Assert.False(blockedConfirmed.Ok);
        Assert.Equal(VehicleOccupancyRules.CannotDelete, blockedConfirmed.Error);

        var assigned = await dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, vehicleId), 2);
        Assert.Null(assigned.Error);
        Assert.Equal(BookingStatuses.Assigned, iso.Db.Bookings.Find(created.BookingId)!.Status);
        Assert.True(await schedule.IsVehicleOccupiedAsync(vehicleId));
        var blockedAssigned = await vehicles.DeleteVehicleAsync(vehicleId);
        Assert.False(blockedAssigned.Ok);
        Assert.Equal(VehicleOccupancyRules.CannotDelete, blockedAssigned.Error);
    }

    [Fact]
    public async Task Completed_and_cancelled_do_not_occupy()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, payments, dispatch, _, _, schedule) = Services(iso);
        var completedId = AddSpare(iso, "51Z-COMP01");
        iso.SetLastCompletedMaintenance(completedId, DateTime.UtcNow.AddDays(-10), 1000);
        var completedBooking = await bookings.CreateBookingAsync(3, SelfDrive(completedId));
        await payments.CreateAsync(3, Deposit(completedBooking!.BookingId));
        await dispatch.ConfirmBookingAsync(completedBooking.BookingId, 2);
        await dispatch.AssignTripAsync(completedBooking.BookingId, new AssignTripRequest(null, completedId), 2);
        await dispatch.HandoverSelfDriveAsync(completedBooking.BookingId, 2, null);
        await dispatch.CompleteSelfDriveAsync(completedBooking.BookingId, 2, null);
        Assert.Equal(BookingStatuses.Completed, iso.Db.Bookings.Find(completedBooking.BookingId)!.Status);
        Assert.False(await schedule.IsVehicleOccupiedAsync(completedId));

        var cancelledId = AddSpare(iso, "51Z-CANC01");
        iso.SetLastCompletedMaintenance(cancelledId, DateTime.UtcNow.AddDays(-10), 1000);
        var cancelledBooking = await bookings.CreateBookingAsync(3, SelfDrive(cancelledId));
        await payments.CreateAsync(3, Deposit(cancelledBooking!.BookingId));
        await bookings.UpdateStatusAsync(cancelledBooking.BookingId, BookingStatuses.Cancelled, 2, "huy");
        Assert.Equal(BookingStatuses.Cancelled, iso.Db.Bookings.Find(cancelledBooking.BookingId)!.Status);
        Assert.Null(iso.Db.Bookings.Find(cancelledBooking.BookingId)!.AssignedVehicleId);
        Assert.False(await schedule.IsVehicleOccupiedAsync(cancelledId));
    }

    [Fact]
    public async Task Inactive_is_blocked_while_held_or_on_open_trip()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, payments, dispatch, drivers, vehicles, _) = Services(iso);
        var holdId = AddSpare(iso, "51Z-HOLD01");
        iso.SetLastCompletedMaintenance(holdId, DateTime.UtcNow.AddDays(-10), 1000);
        var held = await bookings.CreateBookingAsync(3, SelfDrive(holdId));
        await payments.CreateAsync(3, Deposit(held!.BookingId));
        var holdInactive = await SetInactiveAsync(iso, vehicles, holdId);
        Assert.Equal(400, holdInactive.StatusCode);
        Assert.Equal(VehicleOccupancyRules.CannotDeactivate, holdInactive.Error);
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(holdId)!.Status);

        var tripId = AddSpare(iso, "51Z-TRIP01");
        iso.SetLastCompletedMaintenance(tripId, DateTime.UtcNow.AddDays(-10), 1000);
        var trip = await bookings.CreateBookingAsync(3, WithDriver());
        await dispatch.ConfirmBookingAsync(trip!.BookingId, 2);
        await dispatch.AssignTripAsync(trip.BookingId, new AssignTripRequest(6, tripId), 2);
        var assignedInactive = await SetInactiveAsync(iso, vehicles, tripId);
        Assert.Equal(400, assignedInactive.StatusCode);
        Assert.Equal(VehicleOccupancyRules.CannotDeactivate, assignedInactive.Error);

        var assignmentId = iso.Db.TripAssignments.Single(t => t.BookingId == trip.BookingId).AssignmentId;
        Assert.True(await drivers.AcceptTripAsync(6, assignmentId));
        Assert.True(await drivers.StartTripAsync(6, assignmentId));
        var inProgressInactive = await SetInactiveAsync(iso, vehicles, tripId);
        Assert.Equal(400, inProgressInactive.StatusCode);
        Assert.Equal(VehicleStatuses.Rented, iso.Db.Vehicles.Find(tripId)!.Status);
    }

    [Fact]
    public async Task Occupied_delete_does_not_remove_related_history()
    {
        using var iso = new IsolatedCarRentalDb();
        var (bookings, payments, _, _, vehicles, _) = Services(iso);
        var vehicleId = AddSpare(iso);
        iso.SetLastCompletedMaintenance(vehicleId, DateTime.UtcNow.AddDays(-10), 1000);
        var created = await bookings.CreateBookingAsync(3, SelfDrive(vehicleId));
        await payments.CreateAsync(3, Deposit(created!.BookingId));
        var contracts = new ContractService(iso.Db);
        await contracts.CreateAsync(3, created.BookingId);
        var bookingsBefore = iso.Db.Bookings.Count();
        var paymentsBefore = iso.Db.Payments.Count();
        var contractsBefore = iso.Db.Contracts.Count();

        var deleted = await vehicles.DeleteVehicleAsync(vehicleId);
        Assert.False(deleted.Ok);
        Assert.Equal(bookingsBefore, iso.Db.Bookings.Count());
        Assert.Equal(paymentsBefore, iso.Db.Payments.Count());
        Assert.Equal(contractsBefore, iso.Db.Contracts.Count());
        Assert.Equal(created.BookingId, iso.Db.Bookings.Find(created.BookingId)!.BookingId);
        Assert.NotNull(iso.Db.Vehicles.Find(vehicleId));
    }

    [Theory]
    [InlineData("customer1@gmail.com")]
    [InlineData("driver1@carrental.vn")]
    [InlineData("dispatcher@carrental.vn")]
    public async Task Non_admin_cannot_delete_or_inactivate_vehicle(string email)
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var token = await LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Delete, "/api/vehicles/2", token))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.SendAsync(Authed(HttpMethod.Put, "/api/vehicles/2", token,
                JsonContent.Create(new UpdateVehicleRequest(
                    1, "51B-67890", "Hyundai", "Accent", 2023, "Bạc", VehicleStatuses.Inactive, 22000)))))
            .StatusCode);
    }

    [Fact]
    public async Task Concurrent_hold_and_delete_do_not_leave_hold_on_deleted_vehicle()
    {
        using var iso = new IsolatedCarRentalDb();
        var vehicleId = AddSpare(iso);
        iso.SetLastCompletedMaintenance(vehicleId, DateTime.UtcNow.AddDays(-10), 1000);
        var created = await new BookingService(iso.Db, new PricingService())
            .CreateBookingAsync(3, new(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.SelfDrive));
        iso.Db.ChangeTracker.Clear();
        var path = iso.Path;
        var bookingId = created!.BookingId;

        async Task<(bool Ok, string? Error, int StatusCode)> DeleteAsync()
        {
            await using var db = Open(path);
            return await new VehicleService(db).DeleteVehicleAsync(vehicleId);
        }

        async Task<(PaymentResponse? Payment, string? Error, int StatusCode)> HoldAsync()
        {
            await using var db = Open(path);
            return await new PaymentService(db, new ScheduleConflictService(db))
                .CreateAsync(3, new CreatePaymentRequest(
                    bookingId, PaymentTypes.Deposit, PaymentMethods.Cash, null, vehicleId));
        }

        var deleteTask = DeleteAsync();
        var holdTask = HoldAsync();
        await Task.WhenAll(deleteTask, holdTask);
        var deleted = await deleteTask;
        var held = await holdTask;

        await using var verify = Open(path);
        var vehicle = verify.Vehicles.Find(vehicleId);
        var booking = verify.Bookings.Find(bookingId)!;
        var payments = verify.Payments.Count(p => p.BookingId == bookingId);

        Assert.False(vehicle is null && held.Payment is not null);
        if (vehicle is null)
        {
            Assert.True(deleted.Ok);
            Assert.Null(held.Payment);
            Assert.Equal(400, held.StatusCode);
            Assert.Equal(0, payments);
            Assert.Null(booking.AssignedVehicleId);
        }
        else if (held.Payment is not null)
        {
            Assert.False(deleted.Ok);
            Assert.Equal(201, held.StatusCode);
            Assert.Equal(vehicleId, booking.AssignedVehicleId);
            Assert.Equal(1, payments);
        }
    }

    [Fact]
    public async Task Maintenance_status_is_not_blocked_by_occupancy_rule()
    {
        using var iso = new IsolatedCarRentalDb();
        var original = iso.Db.Vehicles.AsNoTracking().Single(v => v.VehicleId == 2);
        var (data, error, status) = await Vehicles(iso.Db).UpdateVehicleAsync(2, new UpdateVehicleRequest(
            original.TypeId, original.LicensePlate, original.Brand, original.Model, original.Year,
            original.Color, VehicleStatuses.Maintenance, original.CurrentKm));
        Assert.Equal(200, status);
        Assert.Null(error);
        Assert.Equal(VehicleStatuses.Maintenance, data!.Status);
    }

    private static int AddSpare(IsolatedCarRentalDb iso, string plate = "51Z-SPARE1")
    {
        iso.Db.Vehicles.Add(new Vehicle
        {
            TypeId = 1,
            LicensePlate = plate,
            Brand = "Honda",
            Model = "City",
            Year = 2024,
            Status = VehicleStatuses.Available,
            CurrentKm = 1000,
            CreatedAt = DateTime.UtcNow
        });
        iso.Db.SaveChanges();
        return iso.Db.Vehicles.Single(v => v.LicensePlate == plate).VehicleId;
    }

    private static VehicleService Vehicles(CarRentalDbContext db) => new(db);

    private static (
        BookingService Bookings,
        PaymentService Payments,
        DispatchService Dispatch,
        DriverService Drivers,
        VehicleService Vehicles,
        ScheduleConflictService Schedule)
        Services(IsolatedCarRentalDb iso)
    {
        var schedule = new ScheduleConflictService(iso.Db);
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees);
        var dispatch = new DispatchService(iso.Db, bookings, drivers, inspections, fees, schedule);
        return (bookings, new PaymentService(iso.Db, schedule), dispatch, drivers, new VehicleService(iso.Db), schedule);
    }

    private static async Task<(VehicleResponse? Data, string? Error, int StatusCode)> SetInactiveAsync(
        IsolatedCarRentalDb iso, VehicleService vehicles, int vehicleId)
    {
        var vehicle = iso.Db.Vehicles.AsNoTracking().Single(v => v.VehicleId == vehicleId);
        return await vehicles.UpdateVehicleAsync(vehicleId, new UpdateVehicleRequest(
            vehicle.TypeId, vehicle.LicensePlate, vehicle.Brand, vehicle.Model, vehicle.Year,
            vehicle.Color, VehicleStatuses.Inactive, vehicle.CurrentKm));
    }

    private static CarRentalDbContext Open(string path)
    {
        var options = new DbContextOptionsBuilder<CarRentalDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        return new CarRentalDbContext(options);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
