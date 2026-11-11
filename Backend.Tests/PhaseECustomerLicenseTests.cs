using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Bookings;
using Backend.DTOs.Customers;
using Backend.Entities;
using Backend.Services;
using Xunit;

namespace Backend.Tests;

/// <summary>plan1.md Phần E — self-drive customer licence (E-T01 … E-T07).</summary>
public sealed class PhaseECustomerLicenseTests : IDisposable
{
    private const int DispatcherId = 2;
    private static readonly DateTime Start = DateTime.Today.AddDays(60).AddHours(9);
    private static readonly DateTime End = Start.AddDays(1);
    private readonly string docRoot = Path.Combine(Path.GetTempPath(), $"crs-docs-e-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { if (Directory.Exists(docRoot)) Directory.Delete(docRoot, true); } catch { /* temp */ }
    }

    private static byte[] Jpeg() => [0xFF, 0xD8, 0xFF, 0xE0, 9, 9];

    [Fact]
    public async Task E01_class_B_customer_sees_4_7_9_seats_but_not_the_16_seat_van()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);

        var licence = await s.Licences.GetAsync(3, End);

        Assert.All(licence!.VehicleTypes.Where(t => t.SeatCapacity <= 9), t => Assert.True(t.CanDrive));
        var van = Assert.Single(licence.VehicleTypes, t => t.SeatCapacity == 16);
        Assert.False(van.CanDrive);
        Assert.Equal("Bằng B không lái được xe 16 chỗ (cần D1 trở lên).", van.Reason);
        Assert.Equal("Bằng B không lái được xe 16 chỗ (cần D1 trở lên).",
            (await s.Bookings.TryCreateBookingAsync(3, SelfDrive(typeId: 3))).Error);
    }

    [Fact]
    public async Task E02_licence_expiring_before_return_cannot_book_self_drive()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        iso.Db.Customers.Find(3)!.DriverLicenseExpiry = DateOnly.FromDateTime(Start);
        iso.Db.SaveChanges();

        Assert.Equal(CustomerLicenseService.ExpiresBeforeReturn, (await s.Bookings.TryCreateBookingAsync(3, SelfDrive())).Error);
    }

    [Fact]
    public async Task E03_customer_without_licence_is_asked_for_one_only_for_self_drive()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var customerId = AddCustomer(iso);

        Assert.Equal("Hãy cung cấp bằng lái phù hợp với xe bạn chọn (xe 4 chỗ cần bằng B trở lên).",
            (await s.Bookings.TryCreateBookingAsync(customerId, SelfDrive())).Error);
        var withDriver = await s.Bookings.TryCreateBookingAsync(customerId,
            new CreateBookingRequest(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.WithDriver));
        Assert.Null(withDriver.Error);
    }

    [Fact]
    public async Task E04_E05_pending_licence_books_but_confirm_waits_for_approval_once_per_customer()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var customerId = AddCustomer(iso);

        // New B1 (motorcycle) refused; old B1 (before 2025) accepted for a self-drive customer.
        Assert.Equal(CustomerLicenseService.MotorcycleLicence, (await s.Licences.UpdateAsync(customerId, Licence("B1"))).Error);
        var (declared, declareError) = await s.Licences.UpdateAsync(customerId, Licence("B1-cu"));
        Assert.True(declared is not null, declareError);
        Assert.Equal(CustomerLicenseStatuses.Pending, declared!.LicenseStatus);

        foreach (var type in new[] { UserDocumentTypes.LicenseFront, UserDocumentTypes.LicenseBack })
            await s.Docs.UploadAsync(customerId, type, new MemoryStream(Jpeg()), Jpeg().Length);
        await s.Licences.NotifyDispatchersIfReadyAsync(customerId);
        Assert.Contains(iso.Db.Notifications, n => n.UserId == DispatcherId && n.Message.Contains("vừa gửi ảnh bằng lái, cần duyệt"));

        var (booking, bookingError) = await s.Bookings.TryCreateBookingAsync(customerId, SelfDrive());
        Assert.True(booking is not null, bookingError);
        Assert.Equal(CustomerLicenseStatuses.Pending, booking!.CustomerLicenseStatus);
        Assert.Equal(CustomerLicenseService.AwaitingApproval, (await s.Dispatch.ConfirmBookingAsync(booking.BookingId, DispatcherId)).Error);

        var (approved, approveError) = await s.Licences.ReviewAsync(customerId, DispatcherId, true, null);
        Assert.True(approved is not null, approveError);
        Assert.Equal(CustomerLicenseStatuses.Approved, approved!.LicenseStatus);
        Assert.Equal("Trần Thị Điều Phối", approved.ReviewedByName);
        Assert.All(approved.Documents, d => Assert.Equal(UserDocumentStatuses.Approved, d.Status));
        Assert.Contains(iso.Db.Notifications, n => n.UserId == customerId && n.Title == "Bằng lái đã được chấp nhận");
        Assert.Null((await s.Dispatch.ConfirmBookingAsync(booking.BookingId, DispatcherId)).Error);

        // Approved once: the next self-drive booking needs nothing more…
        var next = await s.Bookings.TryCreateBookingAsync(customerId, SelfDrive(daysLater: 10));
        Assert.Equal(CustomerLicenseStatuses.Approved, next.Booking!.CustomerLicenseStatus);
        // …until the licence changes.
        var (changed, _) = await s.Licences.UpdateAsync(customerId, Licence("B") with { LicenseNumber = "790999000111" });
        Assert.Equal(CustomerLicenseStatuses.Pending, changed!.LicenseStatus);
    }

    [Fact]
    public async Task Rejected_licence_tells_the_customer_why_and_blocks_new_self_drive()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var customerId = AddCustomer(iso);
        await s.Licences.UpdateAsync(customerId, Licence("B"));

        Assert.Equal(CustomerLicenseService.ReasonRequired, (await s.Licences.ReviewAsync(customerId, DispatcherId, false, null)).Error);
        var (rejected, _) = await s.Licences.ReviewAsync(customerId, DispatcherId, false, "Ảnh bị lóa");
        Assert.Equal(CustomerLicenseStatuses.Rejected, rejected!.LicenseStatus);
        Assert.Contains(iso.Db.Notifications, n => n.UserId == customerId && n.Title == "Bằng lái chưa được chấp nhận" && n.Message.Contains("Ảnh bị lóa"));
        Assert.Equal(CustomerLicenseService.Rejected, (await s.Bookings.TryCreateBookingAsync(customerId, SelfDrive())).Error);
    }

    [Fact]
    public async Task E06b_handover_checks_the_licence_again_rejected_or_expired_since_confirm_blocks_it()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var (created, error) = await s.Bookings.TryCreateBookingAsync(3, SelfDrive());
        Assert.True(created is not null, error);
        Assert.Null((await s.Dispatch.ConfirmBookingAsync(created!.BookingId, DispatcherId)).Error);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        Assert.Null((await s.Dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, 2), DispatcherId)).Error);
        var customer = iso.Db.Customers.Single(c => c.CustomerId == 3);

        // Rejected after the booking was confirmed → no handover, nothing recorded.
        customer.LicenseStatus = CustomerLicenseStatuses.Rejected;
        iso.Db.SaveChanges();
        var (blocked, blockedError) = await s.Dispatch.HandoverSelfDriveAsync(
            created.BookingId, DispatcherId, TestDispatchReady.InspectionForBooking(iso.Db, created.BookingId));
        Assert.Null(blocked);
        Assert.StartsWith(DispatchService.LicenceBlocksHandoverPrefix, blockedError);
        iso.Db.ChangeTracker.Clear();
        Assert.Equal(BookingStatuses.Assigned, iso.Db.Bookings.Find(created.BookingId)!.Status);
        Assert.DoesNotContain(iso.Db.VehicleInspections, i =>
            i.BookingId == created.BookingId && i.InspectionType == VehicleInspectionTypes.Handover);

        // Approved again but the licence now ends before the return date → still no handover.
        customer = iso.Db.Customers.Single(c => c.CustomerId == 3);
        customer.LicenseStatus = CustomerLicenseStatuses.Approved;
        customer.DriverLicenseExpiry = DateOnly.FromDateTime(Start.AddDays(-1));
        iso.Db.SaveChanges();
        Assert.Equal(DispatchService.LicenceBlocksHandoverPrefix + CustomerLicenseService.ExpiresBeforeReturn,
            (await s.Dispatch.HandoverSelfDriveAsync(created.BookingId, DispatcherId,
                TestDispatchReady.InspectionForBooking(iso.Db, created.BookingId))).Error);

        // Valid licence → handover goes through.
        customer.DriverLicenseExpiry = DateOnly.FromDateTime(End.AddYears(5));
        iso.Db.SaveChanges();
        iso.Db.ChangeTracker.Clear();
        var (handed, handError) = await s.Dispatch.HandoverSelfDriveAsync(
            created.BookingId, DispatcherId, TestDispatchReady.InspectionForBooking(iso.Db, created.BookingId));
        Assert.True(handed is not null, handError);
        Assert.Equal(BookingStatuses.InProgress, handed!.Status);
    }

    [Fact]
    public async Task E06_handover_requires_the_original_licence_check_and_records_who_did_it()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var (created, error) = await s.Bookings.TryCreateBookingAsync(3, SelfDrive());
        Assert.True(created is not null, error);
        Assert.Null((await s.Dispatch.ConfirmBookingAsync(created!.BookingId, DispatcherId)).Error);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        Assert.Null((await s.Dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, 2), DispatcherId)).Error);

        var unchecked_ = TestDispatchReady.InspectionForBooking(iso.Db, created.BookingId) with { OriginalLicenseChecked = false };
        Assert.Equal(DispatchService.OriginalLicenceNotChecked,
            (await s.Dispatch.HandoverSelfDriveAsync(created.BookingId, DispatcherId, unchecked_)).Error);
        Assert.Equal(BookingStatuses.Assigned, iso.Db.Bookings.Find(created.BookingId)!.Status);

        var (handed, handError) = await s.Dispatch.HandoverSelfDriveAsync(
            created.BookingId, DispatcherId, TestDispatchReady.InspectionForBooking(iso.Db, created.BookingId));
        Assert.True(handed is not null, handError);
        Assert.Equal(BookingStatuses.InProgress, handed!.Status);
        Assert.Equal("Trần Thị Điều Phối", handed.OriginalLicenseCheckedByName);
        Assert.NotNull(handed.OriginalLicenseCheckedAt);
    }

    [Fact]
    public async Task E07_backfill_gives_the_sample_customers_an_approved_B_licence()
    {
        using var iso = new IsolatedCarRentalDb();
        foreach (var c in iso.Db.Customers)
        {
            c.DriverLicenseNumber = null;
            c.DriverLicenseClass = null;
            c.DriverLicenseExpiry = null;
            c.LicenseStatus = CustomerLicenseStatuses.None;
        }
        var other = AddCustomer(iso);
        iso.Db.SaveChanges();

        DriverLicenseBackfill.Run(iso.Db);

        Assert.Equal(CustomerLicenseStatuses.Approved, iso.Db.Customers.Find(3)!.LicenseStatus);
        Assert.Equal("B", iso.Db.Customers.Find(4)!.DriverLicenseClass);
        Assert.Equal(CustomerLicenseStatuses.None, iso.Db.Customers.Find(other)!.LicenseStatus);
        var s = Services(iso);
        Assert.Null((await s.Bookings.TryCreateBookingAsync(3, SelfDrive())).Error);
    }

    // ---------- helpers ----------

    private static CreateBookingRequest SelfDrive(int typeId = 1, int daysLater = 0) => new(
        typeId, "A", "B", null, null, null, null, Start.AddDays(daysLater), End.AddDays(daysLater), 20, null, RentalModes.SelfDrive);

    private static UpdateCustomerLicenseRequest Licence(string cls) => new(
        "790555444333", cls, DateOnly.FromDateTime(DateTime.Today.AddYears(-4)), DateOnly.FromDateTime(DateTime.Today.AddYears(6)));

    private static int AddCustomer(IsolatedCarRentalDb iso)
    {
        var user = new User
        {
            Email = $"khach{Guid.NewGuid():N}@gmail.com", PasswordHash = "x", FullName = "Khách Mới",
            Phone = "09" + Random.Shared.Next(10000000, 99999999), RoleId = 3, IsActive = true, IsEmailVerified = true, CreatedAt = DateTime.UtcNow
        };
        iso.Db.Users.Add(user);
        iso.Db.SaveChanges();
        iso.Db.Customers.Add(new Customer { CustomerId = user.UserId });
        iso.Db.SaveChanges();
        return user.UserId;
    }

    private sealed record Svc(BookingService Bookings, DispatchService Dispatch, CustomerLicenseService Licences, UserDocumentService Docs);

    private Svc Services(IsolatedCarRentalDb iso)
    {
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees, new ScheduleConflictService(iso.Db));
        var dispatch = new DispatchService(iso.Db, bookings, drivers, inspections, fees, new ScheduleConflictService(iso.Db));
        var docs = new UserDocumentService(iso.Db, new DocumentStorage(docRoot));
        return new Svc(bookings, dispatch, new CustomerLicenseService(iso.Db, docs), docs);
    }
}
