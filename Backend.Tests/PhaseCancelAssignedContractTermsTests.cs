using Backend.Constants;
using Backend.DTOs.Bookings;
using Backend.DTOs.Realtime;
using Backend.Entities;
using Backend.Options;
using Backend.Services;
using Backend.Validation;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// plan1.md — Task A (cancel Assigned before the trip starts) and Task C (cancel-policy / contract terms snapshot).
/// </summary>
public class PhaseCancelAssignedContractTermsTests
{
    private const int Customer = 3;
    private const int OtherCustomer = 4;
    private const int Dispatcher = 2;
    private const int Accent = 2;

    // ---------- Task A: exact 24h boundary (pure function, "now" injected) ----------

    private static readonly DateTime StartVn = new(2026, 11, 20, 8, 0, 0);

    [Fact]
    public void Exactly_24h_before_start_is_allowed()
        => Assert.True(CancelPolicy.CanCustomerCancelAssigned(StartVn, StartVn.AddHours(-24), 24));

    [Fact]
    public void One_second_inside_24h_is_rejected()
        => Assert.False(CancelPolicy.CanCustomerCancelAssigned(StartVn, StartVn.AddHours(-24).AddSeconds(1), 24));

    [Fact]
    public void One_second_outside_24h_is_allowed()
        => Assert.True(CancelPolicy.CanCustomerCancelAssigned(StartVn, StartVn.AddHours(-24).AddSeconds(-1), 24));

    [Fact]
    public void Has_started_at_start_time_but_not_one_second_before()
    {
        Assert.True(CancelPolicy.HasStarted(StartVn, StartVn));
        Assert.False(CancelPolicy.HasStarted(StartVn, StartVn.AddSeconds(-1)));
    }

    [Fact]
    public void Boundary_uses_vietnam_wall_clock_not_utc()
    {
        // 01:00 UTC on 19/11 is 08:00 in Vietnam: exactly 24h before an 08:00 20/11 pickup.
        var utcNow = new DateTime(2026, 11, 19, 1, 0, 0, DateTimeKind.Utc);
        var vnNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, VietnamTime.Zone);

        Assert.Equal(new DateTime(2026, 11, 19, 8, 0, 0), vnNow);
        Assert.True(CancelPolicy.CanCustomerCancelAssigned(StartVn, vnNow, 24));
        // Comparing against UTC by mistake would see 31h and hide the off-by-7h bug.
        Assert.Equal(TimeSpan.FromHours(31), StartVn - DateTime.SpecifyKind(utcNow, DateTimeKind.Unspecified));
        Assert.False(CancelPolicy.CanCustomerCancelAssigned(StartVn, vnNow.AddSeconds(1), 24));
    }

    // ---------- Task A: releasing the actual vehicle ----------

    [Fact]
    public async Task Customer_cancels_with_driver_booking_held_only_by_trip_assignment()
    {
        using var iso = new IsolatedCarRentalDb();
        var capture = new CapturingRealtimePublisher();
        var s = Services(iso, capture);
        var id = await AssignedAsync(iso, s, RentalModes.WithDriver, hoursAhead: 48);
        Assert.Null(iso.Db.Bookings.Find(id)!.AssignedVehicleId);
        Assert.Equal(VehicleStatuses.Rented, iso.Db.Vehicles.Find(Accent)!.Status);
        capture.Clear();

        var (data, error, status) = await s.Bookings.CancelByCustomerAsync(id, Customer, "Đổi kế hoạch");

        Assert.Null(error);
        Assert.Equal(200, status);
        Assert.Equal(BookingStatuses.Cancelled, data!.Status);
        Assert.Equal(TripAssignmentStatuses.Cancelled, iso.Db.TripAssignments.Single(t => t.BookingId == id).Status);
        Assert.Equal(DriverStatuses.Available, iso.Db.Drivers.Find(6)!.Status);
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(Accent)!.Status);
        Assert.Equal(ContractStatuses.Voided, iso.Db.Contracts.Single(c => c.BookingId == id).Status);
        Assert.Contains(iso.Db.BookingStatusHistories, h =>
            h.BookingId == id && h.NewStatus == BookingStatuses.Cancelled
            && h.Note!.Contains(CancelPolicy.ManualDepositRefundNote));
        Assert.Contains(capture.Events, e => e.EventType == RealtimeEventTypes.AssignmentChanged && e.BookingId == id);
        Assert.Contains(capture.Events, e => e.EventType == RealtimeEventTypes.DriverStatusChanged && e.EntityId == 6);
        Assert.Contains(capture.Events, e => e.EventType == RealtimeEventTypes.VehicleStatusChanged && e.EntityId == Accent);
    }

    [Fact]
    public async Task Customer_cancels_self_drive_booking_held_by_assigned_vehicle()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var id = await AssignedAsync(iso, s, RentalModes.SelfDrive, hoursAhead: 48);
        Assert.Equal(Accent, iso.Db.Bookings.Find(id)!.AssignedVehicleId);

        var (_, error, status) = await s.Bookings.CancelByCustomerAsync(id, Customer, "Không đi nữa");

        Assert.Null(error);
        Assert.Equal(200, status);
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(Accent)!.Status);
    }

    [Theory]
    [InlineData(RentalModes.WithDriver)] // other booking holds the car through TripAssignment.VehicleId
    [InlineData(RentalModes.SelfDrive)]  // other booking holds the car through AssignedVehicleId
    public async Task Vehicle_stays_rented_while_another_active_booking_uses_it(string otherMode)
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        MakeDriverAvailable(iso, 7);
        var id = await AssignedAsync(iso, s, RentalModes.WithDriver, hoursAhead: 48);
        var other = await AssignedAsync(iso, s, otherMode, hoursAhead: 120, driverId: 7, customerId: OtherCustomer);

        var (_, error, _) = await s.Bookings.UpdateStatusAsync(id, BookingStatuses.Cancelled, Dispatcher, "test");

        Assert.Null(error);
        Assert.Equal(BookingStatuses.Assigned, iso.Db.Bookings.Find(other)!.Status);
        Assert.Equal(VehicleStatuses.Rented, iso.Db.Vehicles.Find(Accent)!.Status);
    }

    [Fact]
    public async Task Vehicle_released_when_other_booking_on_it_is_finished()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var id = await AssignedAsync(iso, s, RentalModes.WithDriver, hoursAhead: 48);
        iso.Db.Bookings.Add(new Booking
        {
            CustomerId = OtherCustomer, VehicleTypeId = 1, PickupAddress = "A", DropoffAddress = "B",
            StartDate = VietnamTime.Now.AddDays(-5), EndDate = VietnamTime.Now.AddDays(-4),
            TotalAmount = 1, Status = BookingStatuses.Cancelled, RentalMode = RentalModes.SelfDrive,
            AssignedVehicleId = Accent, CreatedAt = DateTime.UtcNow
        });
        iso.Db.SaveChanges();

        await s.Bookings.UpdateStatusAsync(id, BookingStatuses.Cancelled, Dispatcher, "test");

        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(Accent)!.Status);
    }

    [Fact]
    public async Task Vehicle_in_maintenance_is_not_flipped_to_available()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var id = await AssignedAsync(iso, s, RentalModes.WithDriver, hoursAhead: 48);
        iso.Db.Vehicles.Find(Accent)!.Status = VehicleStatuses.Maintenance;
        iso.Db.SaveChanges();

        await s.Bookings.UpdateStatusAsync(id, BookingStatuses.Cancelled, Dispatcher, "test");

        Assert.Equal(VehicleStatuses.Maintenance, iso.Db.Vehicles.Find(Accent)!.Status);
    }

    [Fact]
    public async Task Driver_completing_trip_keeps_vehicle_rented_for_next_booking()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        MakeDriverAvailable(iso, 7);
        var first = await AssignedAsync(iso, s, RentalModes.WithDriver, hoursAhead: 48);
        await AssignedAsync(iso, s, RentalModes.WithDriver, hoursAhead: 120, driverId: 7, customerId: OtherCustomer);
        var assignmentId = iso.Db.TripAssignments.Single(t => t.BookingId == first).AssignmentId;

        Assert.True(await s.Drivers.AcceptTripAsync(6, assignmentId));
        Assert.True(await s.Drivers.StartTripAsync(6, assignmentId));
        var done = await s.Drivers.CompleteTripAsync(
            6, assignmentId, TestDispatchReady.InspectionForBooking(iso.Db, first, extraKm: 30));

        Assert.True(done.Ok, done.Error);
        Assert.Equal(VehicleStatuses.Rented, iso.Db.Vehicles.Find(Accent)!.Status);
    }

    // ---------- Task A: window, roles, start time ----------

    [Fact]
    public async Task Customer_inside_window_is_rejected_but_dispatcher_can_cancel()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var id = await AssignedAsync(iso, s, RentalModes.WithDriver, hoursAhead: 23);

        var view = await s.Bookings.GetBookingByIdAsync(id);
        Assert.False(view!.CustomerCanCancel);

        var (data, error, status) = await s.Bookings.CancelByCustomerAsync(id, Customer, "Muộn");
        Assert.Null(data);
        Assert.Equal(400, status);
        Assert.Contains("24 giờ", error);
        Assert.Equal(BookingStatuses.Assigned, iso.Db.Bookings.Find(id)!.Status);

        var (byDispatcher, dispatcherError, dispatcherStatus) =
            await s.Bookings.UpdateStatusAsync(id, BookingStatuses.Cancelled, Dispatcher, "Điều phối hủy");
        Assert.Null(dispatcherError);
        Assert.Equal(200, dispatcherStatus);
        Assert.Equal(BookingStatuses.Cancelled, byDispatcher!.Status);
        Assert.Equal(VehicleStatuses.Available, iso.Db.Vehicles.Find(Accent)!.Status);
    }

    [Fact]
    public async Task Response_flag_and_deadline_follow_window()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var id = await AssignedAsync(iso, s, RentalModes.SelfDrive, hoursAhead: 48);

        var view = await s.Bookings.GetBookingByIdAsync(id);

        Assert.True(view!.CustomerCanCancel);
        Assert.Equal(view.StartDate.AddHours(-24), view.CustomerCancelDeadline);
    }

    [Fact]
    public async Task Nobody_cancels_assigned_booking_after_start_time()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var id = await AssignedAsync(iso, s, RentalModes.WithDriver, hoursAhead: 48);
        var booking = iso.Db.Bookings.Find(id)!;
        booking.StartDate = VietnamTime.Now.AddMinutes(-5);
        iso.Db.SaveChanges();

        var customer = await s.Bookings.CancelByCustomerAsync(id, Customer, "Trễ");
        var dispatcher = await s.Bookings.UpdateStatusAsync(id, BookingStatuses.Cancelled, Dispatcher, "Trễ");

        Assert.Equal((400, CancelPolicy.AlreadyStarted), (customer.StatusCode, customer.Error));
        Assert.Equal((400, CancelPolicy.AlreadyStarted), (dispatcher.StatusCode, dispatcher.Error));
        Assert.Equal(BookingStatuses.Assigned, iso.Db.Bookings.Find(id)!.Status);
        Assert.Equal(VehicleStatuses.Rented, iso.Db.Vehicles.Find(Accent)!.Status);
    }

    [Fact]
    public async Task Customer_cannot_cancel_someone_elses_assigned_booking()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var id = await AssignedAsync(iso, s, RentalModes.WithDriver, hoursAhead: 48);

        var (_, error, status) = await s.Bookings.CancelByCustomerAsync(id, OtherCustomer, "Không phải của tôi");

        Assert.Equal(403, status);
        Assert.Equal(BookingService.NotOwner, error);
    }

    // ---------- Task C: snapshots ----------

    [Fact]
    public async Task Booking_and_contract_snapshot_policy_and_exact_wording()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var id = await ConfirmedAsync(iso, s);

        Assert.Equal(24, iso.Db.Bookings.Find(id)!.QuotedCancelMinHours);
        var (contract, _, error, _) = await s.Contracts.CreateAsync(Customer, id);

        Assert.Null(error);
        Assert.Equal(24, contract!.CancelMinHours);
        Assert.Equal(ContractTerms.Build(24), contract.Terms);
        Assert.Contains(contract.Terms!, t => t.Contains("tối thiểu 24 giờ"));
    }

    [Fact]
    public async Task Changing_policy_does_not_change_issued_contract_or_its_cancel_rule()
    {
        using var iso = new IsolatedCarRentalDb();
        var at24 = Services(iso);
        var id = await AssignedAsync(iso, at24, RentalModes.SelfDrive, hoursAhead: 30, issueRealContract: true);
        var issued = iso.Db.Contracts.Single(c => c.BookingId == id);
        var termsBefore = issued.TermsSnapshot;

        var at48 = Services(iso, policyHours: 48);
        var (contract, _, _) = await at48.Contracts.GetByBookingAsync(Customer, RoleNames.Customer, id);
        Assert.Equal(24, contract!.CancelMinHours);
        Assert.Contains(contract.Terms!, t => t.Contains("tối thiểu 24 giờ"));
        Assert.True((await at48.Bookings.GetBookingByIdAsync(id))!.CustomerCanCancel);

        // 30h left: allowed under the signed 24h rule even though current policy says 48h.
        var (_, error, status) = await at48.Bookings.CancelByCustomerAsync(id, Customer, "Hủy theo hợp đồng");
        Assert.Null(error);
        Assert.Equal(200, status);
        Assert.Equal(termsBefore, iso.Db.Contracts.Single(c => c.BookingId == id).TermsSnapshot);
        Assert.Equal(24, iso.Db.Contracts.Single(c => c.BookingId == id).CancelMinHoursSnapshot);
    }

    [Fact]
    public async Task Booking_created_before_policy_change_keeps_old_window_even_without_contract()
    {
        using var iso = new IsolatedCarRentalDb();
        var id = await ConfirmedAsync(iso, Services(iso));

        var at48 = Services(iso, policyHours: 48);
        var (contract, _, _, _) = await at48.Contracts.CreateAsync(Customer, id);
        Assert.Equal(24, contract!.CancelMinHours);
        Assert.Contains(contract.Terms!, t => t.Contains("tối thiểu 24 giờ"));

        var newer = await ConfirmedAsync(iso, at48);
        Assert.Equal(48, iso.Db.Bookings.Find(newer)!.QuotedCancelMinHours);
    }

    [Fact]
    public async Task Signing_and_voiding_never_rewrite_the_snapshot()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var id = await ConfirmedAsync(iso, s);
        var (created, _, _, _) = await s.Contracts.CreateAsync(Customer, id);
        var snapshot = iso.Db.Contracts.Single(c => c.BookingId == id).TermsSnapshot;

        await s.Contracts.SimulateSignAsync(Customer, created!.ContractId);
        Assert.Equal(snapshot, iso.Db.Contracts.Single(c => c.BookingId == id).TermsSnapshot);

        await s.Bookings.UpdateStatusAsync(id, BookingStatuses.Cancelled, Dispatcher, "void");
        var stored = iso.Db.Contracts.Single(c => c.BookingId == id);
        Assert.Equal(ContractStatuses.Voided, stored.Status);
        Assert.Equal(snapshot, stored.TermsSnapshot);
        Assert.Equal(24, stored.CancelMinHoursSnapshot);
    }

    [Fact]
    public void Legacy_contract_without_snapshot_maps_to_null_terms()
    {
        var legacy = new Contract { ContractNumber = "CTR-1", TermsSnapshot = null };
        var mapped = ContractService.Map(legacy);
        Assert.Null(mapped.Terms);
        Assert.Null(mapped.CancelMinHours);
    }

    [Fact]
    public void Voided_contract_snapshot_is_ignored_when_resolving_window()
    {
        var booking = new Booking { QuotedCancelMinHours = 12 };
        Assert.Equal(36, CancelPolicy.ResolveMinHours(booking,
            new Contract { Status = ContractStatuses.Signed, CancelMinHoursSnapshot = 36 }, 24));
        Assert.Equal(12, CancelPolicy.ResolveMinHours(booking,
            new Contract { Status = ContractStatuses.Voided, CancelMinHoursSnapshot = 36 }, 24));
        Assert.Equal(24, CancelPolicy.ResolveMinHours(new Booking(), null, 24));
    }

    // ---------- helpers ----------

    private sealed record Svc(BookingService Bookings, DispatchService Dispatch, DriverService Drivers, ContractService Contracts);

    private static Svc Services(IsolatedCarRentalDb iso, IRealtimePublisher? realtime = null, int policyHours = 24)
    {
        var policy = Microsoft.Extensions.Options.Options.Create(new CancelPolicyOptions { MinHoursBeforeStart = policyHours });
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing, realtime, cancelPolicy: policy);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var schedule = new ScheduleConflictService(iso.Db);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees, schedule, realtime);
        var dispatch = new DispatchService(iso.Db, bookings, drivers, inspections, fees, schedule, realtime);
        return new Svc(bookings, dispatch, drivers, new ContractService(iso.Db, realtime, policy));
    }

    private static CreateBookingRequest Request(string mode, double hoursAhead, int? vehicleId)
    {
        var start = VietnamTime.Now.AddHours(hoursAhead);
        start = new DateTime(start.Year, start.Month, start.Day, start.Hour, start.Minute, 0);
        return new CreateBookingRequest(1, "A", "B", null, null, null, null, start, start.AddHours(10), 20, null,
            mode, mode == RentalModes.SelfDrive ? vehicleId : null);
    }

    private static async Task<int> ConfirmedAsync(IsolatedCarRentalDb iso, Svc s, double hoursAhead = 72)
    {
        var created = await s.Bookings.CreateBookingAsync(Customer, Request(RentalModes.SelfDrive, hoursAhead, null));
        await s.Dispatch.ConfirmBookingAsync(created!.BookingId, Dispatcher);
        return created.BookingId;
    }

    private static async Task<int> AssignedAsync(
        IsolatedCarRentalDb iso, Svc s, string mode, double hoursAhead,
        int? driverId = 6, int customerId = Customer, bool issueRealContract = false)
    {
        var created = await s.Bookings.CreateBookingAsync(customerId, Request(mode, hoursAhead, Accent));
        Assert.NotNull(created);
        await s.Dispatch.ConfirmBookingAsync(created!.BookingId, Dispatcher);
        if (issueRealContract)
            await s.Contracts.CreateAsync(customerId, created.BookingId);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        var assigned = await s.Dispatch.AssignTripAsync(
            created.BookingId,
            new AssignTripRequest(mode == RentalModes.WithDriver ? driverId : null, Accent),
            Dispatcher);
        Assert.Null(assigned.Error);
        return created.BookingId;
    }

    private static void MakeDriverAvailable(IsolatedCarRentalDb iso, int driverId)
    {
        iso.Db.Drivers.Find(driverId)!.Status = DriverStatuses.Available;
        iso.Db.SaveChanges();
    }
}
