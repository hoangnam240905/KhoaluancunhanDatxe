using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Bookings;
using Backend.DTOs.Customers;
using Backend.DTOs.Dispatch;
using Backend.DTOs.Drivers;
using Backend.Entities;
using Backend.Services;
using Backend.Validation;
using Xunit;

namespace Backend.Tests;

/// <summary>
/// Phase 9 — drivers and customers may hold several licences; a vehicle type may be driven when ANY approved
/// licence covers its class and is valid through the return date (M01 … M08).
/// </summary>
public sealed class Phase9MultiLicenceTests : IDisposable
{
    private const int DispatcherId = 2;
    private const int DriverId = 6;      // seed: B2 (old licence), verified
    private const int CustomerId = 3;    // seed: B, approved
    private const int Car4Seat = 1;
    private const int Van16Seat = 3;
    private static readonly DateTime Start = DateTime.Today.AddDays(60).AddHours(9);
    private static readonly DateTime End = Start.AddDays(2);
    private readonly string docRoot = Path.Combine(Path.GetTempPath(), $"crs-docs-9-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { if (Directory.Exists(docRoot)) Directory.Delete(docRoot, true); } catch { /* temp */ }
    }

    [Fact]
    public async Task M01_driver_with_only_B2_is_offered_for_4_seats_not_for_the_16_seat_van()
    {
        using var iso = new IsolatedCarRentalDb();
        var schedule = new ScheduleConflictService(iso.Db);

        Assert.Contains(await schedule.FindAssignableDriversAsync(Start, End, qualifyFor: Type(iso, Car4Seat)), d => d.DriverId == DriverId);
        Assert.DoesNotContain(await schedule.FindAssignableDriversAsync(Start, End, qualifyFor: Type(iso, Van16Seat)), d => d.DriverId == DriverId);
    }

    [Fact]
    public async Task M02_driver_adds_D1_verified_per_licence_then_drives_the_van_and_keeps_B2()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);

        var (added, addError) = await s.Drivers.AddLicenceAsync(DriverId, DriverLicence("790666000111", "D1"));
        Assert.True(added is not null, addError);
        Assert.Equal(2, added!.Licences!.Count);
        var d1 = added.Licences.Single(l => l.Class == "D1");
        Assert.Equal(PersonLicenseStatuses.Pending, d1.Status);
        // Adding a licence does not reset the one already verified.
        Assert.Equal(DriverVerificationStatuses.Verified, added.VerificationStatus);
        Assert.Equal(PersonLicenseStatuses.Approved, added.Licences.Single(l => l.IsPrimary).Status);
        Assert.NotNull(await ReasonForDriverAsync(iso, Van16Seat)); // pending D1 does not count yet

        foreach (var side in new[] { UserDocumentTypes.LicenseFront, UserDocumentTypes.LicenseBack })
            Assert.Null((await s.Docs.UploadAsync(DriverId, side, new MemoryStream(Jpeg()), Jpeg().Length, d1.LicenseId)).Error);
        foreach (var side in new[] { UserDocumentTypes.IdCardFront, UserDocumentTypes.IdCardBack })
            Assert.Null((await s.Docs.UploadAsync(DriverId, side, new MemoryStream(Jpeg()), Jpeg().Length)).Error);

        Assert.Equal(DriverVerificationService.PhotosNotAccepted,
            (await s.Drivers.VerifyLicenceAsync(DriverId, d1.LicenseId, DispatcherId, true, null)).Error);
        var (photos, photoError) = await s.Drivers.ReviewLicencePhotosAsync(DriverId, d1.LicenseId, DispatcherId, true, null);
        Assert.True(photos is not null, photoError);
        Assert.Equal(DriverVerificationService.PhotoAccepted, photos!.Licences!.Single(l => l.LicenseId == d1.LicenseId).PhotoStatus);
        var (verified, verifyError) = await s.Drivers.VerifyLicenceAsync(DriverId, d1.LicenseId, DispatcherId, true, null);
        Assert.True(verified is not null, verifyError);

        Assert.All(verified!.Licences!, l => Assert.Equal(PersonLicenseStatuses.Approved, l.Status));
        Assert.Contains(verified.Licences!, l => l.Class == "B2");
        Assert.Null(await ReasonForDriverAsync(iso, Van16Seat));
        Assert.Null(await ReasonForDriverAsync(iso, Car4Seat));
        Assert.Contains(await new ScheduleConflictService(iso.Db).FindAssignableDriversAsync(Start, End, qualifyFor: Type(iso, Van16Seat)),
            d => d.DriverId == DriverId);
    }

    [Fact]
    public async Task M03_expired_B2_and_valid_D1_still_drives_4_seats()
    {
        using var iso = new IsolatedCarRentalDb();
        PersonLicenseBackfill.Run(iso.Db);
        Primary(iso, LicenceOwnerKinds.Driver, DriverId).Expiry = DateOnly.FromDateTime(Start.AddDays(-1));
        iso.Db.SaveChanges();
        Assert.Equal(DriverQualification.LicenceExpires, await ReasonForDriverAsync(iso, Car4Seat));

        AddApproved(iso, LicenceOwnerKinds.Driver, DriverId, "790666000222", "D1");

        Assert.Null(await ReasonForDriverAsync(iso, Car4Seat));
        Assert.Null(await ReasonForDriverAsync(iso, Van16Seat));
    }

    [Fact]
    public async Task M04_customer_with_B_self_drives_4_seats_and_the_van_once_D1_is_approved()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        Assert.Null((await s.Licences.GetAsync(CustomerId, End))!.VehicleTypes.Single(t => t.TypeId == Car4Seat).Reason);
        Assert.False((await s.Licences.GetAsync(CustomerId, End))!.VehicleTypes.Single(t => t.TypeId == Van16Seat).CanDrive);
        Assert.Equal("Bằng B không lái được xe 16 chỗ (cần D1 trở lên).",
            (await s.Bookings.TryCreateBookingAsync(CustomerId, SelfDrive(Van16Seat))).Error);

        var (added, addError) = await s.Licences.AddLicenceAsync(CustomerId, CustomerLicence("790777000111", "D1"));
        Assert.True(added is not null, addError);
        var d1 = added!.Licences!.Single(l => l.Class == "D1");
        Assert.Equal(CustomerLicenseStatuses.Approved, added.LicenseStatus); // B stays approved

        // Pending D1: the van can be booked (as for a first licence) but not confirmed yet.
        var (booking, bookingError) = await s.Bookings.TryCreateBookingAsync(CustomerId, SelfDrive(Van16Seat));
        Assert.True(booking is not null, bookingError);
        Assert.Equal(CustomerLicenseService.AwaitingApproval, (await s.Dispatch.ConfirmBookingAsync(booking!.BookingId, DispatcherId)).Error);

        foreach (var side in new[] { UserDocumentTypes.LicenseFront, UserDocumentTypes.LicenseBack })
            await s.Docs.UploadAsync(CustomerId, side, new MemoryStream(Jpeg()), Jpeg().Length, d1.LicenseId);
        var (reviewed, reviewError) = await s.Licences.ReviewLicenceAsync(CustomerId, d1.LicenseId, DispatcherId, true, null);
        Assert.True(reviewed is not null, reviewError);
        Assert.True(reviewed!.VehicleTypes.Single(t => t.TypeId == Van16Seat).CanDrive);
        Assert.Contains(reviewed.Licences!, l => l.Class == "B" && l.Status == PersonLicenseStatuses.Approved);
        Assert.Null((await s.Dispatch.ConfirmBookingAsync(booking.BookingId, DispatcherId)).Error);
    }

    [Fact]
    public async Task M05_a_licence_number_belongs_to_one_account()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);

        // Customer 4's number, used by customer 3 or by a driver → refused.
        Assert.Equal(LicenseNumberRules.Taken, (await s.Licences.AddLicenceAsync(CustomerId, CustomerLicence("790400000004", "D1"))).Error);
        Assert.Equal(LicenseNumberRules.Taken, (await s.Drivers.AddLicenceAsync(DriverId, DriverLicence("790400000004", "D1"))).Error);
        Assert.Equal("Số bằng lái này đã được dùng cho một tài khoản khác.", LicenseNumberRules.Taken);
        // Driver 7's number → refused for a customer too.
        Assert.Equal(LicenseNumberRules.Taken, (await s.Licences.AddLicenceAsync(CustomerId, CustomerLicence("790555666777", "D1"))).Error);
        // The same person cannot declare one number twice.
        Assert.Equal(LicenceBook.DuplicateOwn, (await s.Licences.AddLicenceAsync(CustomerId, CustomerLicence("790300000003", "D1"))).Error);
    }

    [Fact]
    public async Task M06_licence_number_must_be_12_digits()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        Assert.Equal(LicenseNumberRules.Invalid, (await s.Licences.AddLicenceAsync(CustomerId, CustomerLicence("12345", "D1"))).Error);
        Assert.Equal(LicenseNumberRules.Invalid, (await s.Drivers.AddLicenceAsync(DriverId, DriverLicence("79066600011A", "D1"))).Error);
        Assert.Single(iso.Db.PersonLicenses.Where(l => l.OwnerKind == LicenceOwnerKinds.Customer && l.OwnerId == CustomerId));
    }

    [Fact]
    public async Task M07_self_drive_handover_passes_when_B_expires_before_return_but_D1_is_valid()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var (created, error) = await s.Bookings.TryCreateBookingAsync(CustomerId, SelfDrive(Car4Seat));
        Assert.True(created is not null, error);
        Assert.Null((await s.Dispatch.ConfirmBookingAsync(created!.BookingId, DispatcherId)).Error);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        Assert.Null((await s.Dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(null, 2), DispatcherId)).Error);

        PersonLicenseBackfill.Run(iso.Db);
        Primary(iso, LicenceOwnerKinds.Customer, CustomerId).Expiry = DateOnly.FromDateTime(Start);
        iso.Db.SaveChanges();
        Assert.Equal(DispatchService.LicenceBlocksHandoverPrefix + CustomerLicenseService.ExpiresBeforeReturn,
            (await s.Dispatch.HandoverSelfDriveAsync(created.BookingId, DispatcherId,
                TestDispatchReady.InspectionForBooking(iso.Db, created.BookingId))).Error);

        AddApproved(iso, LicenceOwnerKinds.Customer, CustomerId, "790777000222", "D1");
        iso.Db.ChangeTracker.Clear();
        var (handed, handError) = await s.Dispatch.HandoverSelfDriveAsync(
            created.BookingId, DispatcherId, TestDispatchReady.InspectionForBooking(iso.Db, created.BookingId));
        Assert.True(handed is not null, handError);
        Assert.Equal(BookingStatuses.InProgress, handed!.Status);
    }

    [Fact]
    public async Task M08_vehicle_change_to_a_type_only_the_drivers_second_licence_covers_is_allowed()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var created = await s.Bookings.CreateBookingAsync(CustomerId,
            new CreateBookingRequest(Car4Seat, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.WithDriver));
        await s.Dispatch.ConfirmBookingAsync(created!.BookingId, DispatcherId);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        Assert.Null((await s.Dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(DriverId, 2), DispatcherId)).Error);

        // No other 4-seat car is free → other types are offered, but only those the driver may drive.
        foreach (var v in iso.Db.Vehicles.Where(v => v.TypeId == Car4Seat && v.VehicleId != 2))
            v.Status = VehicleStatuses.Maintenance;
        iso.Db.SaveChanges();
        var before = (await s.Changes.GetOptionsAsync(created.BookingId)).Data!;
        Assert.Empty(before.SameType);
        Assert.DoesNotContain(before.OtherTypes, o => VanIds(iso).Contains(o.VehicleId));

        PersonLicenseBackfill.Run(iso.Db);
        AddApproved(iso, LicenceOwnerKinds.Driver, DriverId, "790666000333", "D1");
        var after = (await s.Changes.GetOptionsAsync(created.BookingId)).Data!;
        var van = after.OtherTypes.First(o => VanIds(iso).Contains(o.VehicleId));

        var (change, changeError) = await s.Changes.ApplyAsync(created.BookingId, DispatcherId,
            new ChangeVehicleRequest(van.VehicleId, VehicleChangeReasons.NeedChange, null, null, null, null, null, CustomerAgreed: true));
        Assert.True(change is not null, changeError);
    }

    [Fact]
    public void Backfill_copies_every_single_licence_once()
    {
        using var iso = new IsolatedCarRentalDb();
        var added = PersonLicenseBackfill.Run(iso.Db);

        Assert.Equal(iso.Db.Drivers.Count(d => d.LicenseNumber != "" && d.LicenseClass != null)
            + iso.Db.Customers.Count(c => c.LicenseStatus != CustomerLicenseStatuses.None && c.DriverLicenseNumber != null), added);
        Assert.Equal(0, PersonLicenseBackfill.Run(iso.Db));
        var d6 = Primary(iso, LicenceOwnerKinds.Driver, DriverId);
        Assert.Equal(("B2", PersonLicenseStatuses.Approved), (d6.Class, d6.Status));
        Assert.Equal("790300000003", Primary(iso, LicenceOwnerKinds.Customer, CustomerId).Number);
    }

    // ---------- helpers ----------

    private static byte[] Jpeg() => [0xFF, 0xD8, 0xFF, 0xE0, 9, 9];

    private static VehicleType Type(IsolatedCarRentalDb iso, int typeId) => iso.Db.VehicleTypes.Single(t => t.TypeId == typeId);

    private static List<int> VanIds(IsolatedCarRentalDb iso) => iso.Db.Vehicles.Where(v => v.TypeId == Van16Seat).Select(v => v.VehicleId).ToList();

    private static async Task<string?> ReasonForDriverAsync(IsolatedCarRentalDb iso, int typeId)
    {
        iso.Db.ChangeTracker.Clear();
        var driver = iso.Db.Drivers.Single(d => d.DriverId == DriverId);
        await LicenceBook.LoadAsync(iso.Db, driver);
        return DriverQualification.ReasonFor(driver, Type(iso, typeId), End);
    }

    private static PersonLicense Primary(IsolatedCarRentalDb iso, string kind, int ownerId)
        => iso.Db.PersonLicenses.Where(l => l.OwnerKind == kind && l.OwnerId == ownerId).OrderBy(l => l.PersonLicenseId).First();

    private static void AddApproved(IsolatedCarRentalDb iso, string kind, int ownerId, string number, string cls)
    {
        iso.Db.PersonLicenses.Add(new PersonLicense
        {
            OwnerKind = kind, OwnerId = ownerId, Number = number, Class = cls,
            Expiry = DateOnly.FromDateTime(End.AddYears(5)), Status = PersonLicenseStatuses.Approved,
            ReviewedByUserId = DispatcherId, ReviewedByName = "Trần Thị Điều Phối", ReviewedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow
        });
        iso.Db.SaveChanges();
    }

    private static CreateBookingRequest SelfDrive(int typeId) => new(
        typeId, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.SelfDrive);

    private static UpdateCustomerLicenseRequest CustomerLicence(string number, string cls) => new(
        number, cls, DateOnly.FromDateTime(DateTime.Today.AddYears(-1)), DateOnly.FromDateTime(DateTime.Today.AddYears(8)));

    private static UpdateDriverLicenseRequest DriverLicence(string number, string cls) => new(
        number, cls, DateOnly.FromDateTime(DateTime.Today.AddYears(-1)), DateOnly.FromDateTime(DateTime.Today.AddYears(8)), null);

    private sealed record Svc(BookingService Bookings, DispatchService Dispatch, CustomerLicenseService Licences,
        DriverVerificationService Drivers, UserDocumentService Docs, VehicleChangeService Changes);

    private Svc Services(IsolatedCarRentalDb iso)
    {
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees, new ScheduleConflictService(iso.Db));
        var dispatch = new DispatchService(iso.Db, bookings, drivers, inspections, fees, new ScheduleConflictService(iso.Db));
        var docs = new UserDocumentService(iso.Db, new DocumentStorage(docRoot));
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        return new Svc(bookings, dispatch, new CustomerLicenseService(iso.Db, docs),
            new DriverVerificationService(iso.Db, docs, config), docs, new VehicleChangeService(iso.Db, inspections));
    }
}
