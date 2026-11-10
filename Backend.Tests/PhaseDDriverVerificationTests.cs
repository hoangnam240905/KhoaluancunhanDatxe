using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Constants;
using Backend.Data;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.DTOs.Drivers;
using Backend.Entities;
using Backend.Services;
using Backend.Services.Notifications;
using Backend.Validation;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Backend.Tests;

/// <summary>plan1.md Phần D — driver licence verification (D-T01 … D-T07) + Luật 36/2024 licence classes.</summary>
public sealed class PhaseDDriverVerificationTests : IDisposable
{
    private const int DispatcherId = 2;
    private static readonly DateTime Start = DateTime.Today.AddDays(55).AddHours(9);
    private static readonly DateTime End = Start.AddHours(6);
    private readonly string docRoot = Path.Combine(Path.GetTempPath(), $"crs-docs-d-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { if (Directory.Exists(docRoot)) Directory.Delete(docRoot, true); } catch { /* temp */ }
    }

    private static byte[] Jpeg() => [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4];

    [Theory]
    [InlineData("B", 4, null)]
    [InlineData("B", 9, null)]
    [InlineData("B2", 7, null)]
    [InlineData("D1", 16, null)]
    [InlineData("D-cu", 16, null)]
    [InlineData("D2", 29, null)]
    [InlineData("B", 16, "Bằng B không lái được xe 16 chỗ (cần D1 trở lên).")]
    [InlineData("B2", 16, "Bằng B2 (cũ) không lái được xe 16 chỗ (cần D1 trở lên).")]
    [InlineData("D1", 29, "Bằng D1 không lái được xe 29 chỗ (cần D2 trở lên).")]
    [InlineData("B1", 4, "Bằng B1 (cấp từ 2025 — mô tô) không dùng để lái xe kinh doanh vận tải.")]
    [InlineData("B1-cu", 4, "Bằng B1 (cấp trước 01/01/2025) không dùng để lái xe kinh doanh vận tải.")]
    [InlineData(null, 4, "Chưa khai hạng bằng lái.")]
    public void Licence_classes_follow_law_36_2024(string? licence, int seats, string? expected)
        => Assert.Equal(expected, LicenseClasses.CannotDrive(licence, null, seats, professional: true));

    [Fact]
    public void Old_B1_is_fine_for_a_self_drive_customer_but_new_B1_is_not_a_car_licence()
    {
        Assert.Null(LicenseClasses.CannotDrive("B1-cu", "B", 7, professional: false));
        Assert.NotNull(LicenseClasses.CannotDrive("B1", "B", 4, professional: false));
        Assert.Equal("D1", LicenseClasses.MinimumForSeats(16));
        Assert.Equal("B", LicenseClasses.MinimumForSeats(9));
    }

    [Fact]
    public async Task D01_registered_driver_is_pending_gets_guidance_and_cannot_be_assigned()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var reg = await client.PostAsJsonAsync("/api/auth/register-driver",
            new RegisterDriverRequest("Phan Văn Mới", "taixe.moi@carrental.vn", "0933111222", "Password123!", "Password123!"));
        Assert.Equal(HttpStatusCode.OK, reg.StatusCode);
        var auth = await reg.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal(RoleNames.Driver, auth!.Role);

        var me = await (await client.SendAsync(Authed(HttpMethod.Get, "/api/drivers/me/verification", auth.Token)))
            .Content.ReadFromJsonAsync<DriverVerificationResponse>();
        Assert.Equal(DriverVerificationStatuses.Pending, me!.VerificationStatus);
        Assert.Equal(DriverVerificationService.PhotoMissing, me.PhotoStatus);
        Assert.Equal(4, me.MissingDocuments.Count);
        Assert.Null(me.LicenseNumber);

        var notes = await (await client.SendAsync(Authed(HttpMethod.Get, "/api/notifications", auth.Token)))
            .Content.ReadAsStringAsync();
        Assert.Contains("Cập nhật bằng lái và giấy tờ", notes);

        var classes = await client.GetFromJsonAsync<List<LicenseClassOption>>("/api/drivers/license-classes");
        Assert.Contains(classes!, c => c.Code == "D1" && c.AllowedForDrivers);
        Assert.Contains(classes!, c => c.Code == "B1" && !c.AllowedForDrivers);
    }

    [Fact]
    public async Task D02_D05_two_step_review_records_names_and_only_verified_driver_is_assignable()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var driverId = AddPendingDriver(iso);

        // Wrong class / expired licence are refused when the driver declares them.
        Assert.Equal(DriverVerificationService.LicenseClassNotForDrivers,
            (await s.Verify.UpdateLicenseAsync(driverId, Licence("B1-cu"))).Error);
        Assert.Equal(DriverVerificationService.LicenseAlreadyExpired,
            (await s.Verify.UpdateLicenseAsync(driverId, Licence("B") with { LicenseExpiry = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)) })).Error);
        Assert.Null((await s.Verify.UpdateLicenseAsync(driverId, Licence("B"))).Error);

        foreach (var type in UserDocumentService.RequiredFor(RoleNames.Driver))
            await s.Docs.UploadAsync(driverId, type, new MemoryStream(Jpeg()), Jpeg().Length);
        await s.Verify.NotifyDispatchersIfReadyAsync(driverId);
        Assert.Contains(iso.Db.Notifications, n => n.UserId == DispatcherId && n.Title == "Hồ sơ bằng lái cần duyệt");

        // Step 2 is locked until the photos are accepted.
        Assert.Equal(DriverVerificationService.PhotosNotAccepted, (await s.Verify.VerifyAsync(driverId, DispatcherId, true, null)).Error);

        // Step 1: reject with a reason (TX-2), the driver re-sends, then accept (TX-1).
        Assert.Equal(DriverVerificationService.ReasonRequired, (await s.Verify.ReviewPhotosAsync(driverId, DispatcherId, false, null)).Error);
        var (rejected, _) = await s.Verify.ReviewPhotosAsync(driverId, DispatcherId, false, "Ảnh CCCD bị mờ");
        Assert.Equal(DriverVerificationService.PhotoRejected, rejected!.PhotoStatus);
        Assert.Contains(iso.Db.Notifications, n => n.UserId == driverId && n.Title == "Ảnh bằng lái chưa đạt" && n.Message.Contains("Ảnh CCCD bị mờ"));
        foreach (var type in UserDocumentService.RequiredFor(RoleNames.Driver))
            await s.Docs.UploadAsync(driverId, type, new MemoryStream(Jpeg()), Jpeg().Length);
        var (accepted, acceptError) = await s.Verify.ReviewPhotosAsync(driverId, DispatcherId, true, null);
        Assert.True(accepted is not null, acceptError);
        Assert.Equal(DriverVerificationService.PhotoAccepted, accepted!.PhotoStatus);
        Assert.Equal(DriverVerificationStatuses.Pending, accepted.VerificationStatus);
        Assert.All(accepted.Documents, d => Assert.Equal("Trần Thị Điều Phối", d.ReviewedByName));
        Assert.Contains(iso.Db.Notifications, n => n.UserId == driverId && n.Title == "Bằng lái đã được chấp nhận" && n.Message.Contains("7 ngày"));

        // Still pending → cannot be assigned.
        var bookingId = await ConfirmedWithDriverAsync(iso, s, typeId: 1);
        Assert.Equal(DriverQualification.NotVerified,
            (await s.Dispatch.AssignTripAsync(bookingId, new AssignTripRequest(driverId, 1), DispatcherId)).Error);

        // Step 2 fails once (TX-4), driver corrects the licence (back to pending), then verified (TX-3).
        var (failed, _) = await s.Verify.VerifyAsync(driverId, DispatcherId, false, "Số bằng gốc khác số đã khai");
        Assert.Equal(DriverVerificationStatuses.Rejected, failed!.VerificationStatus);
        Assert.Contains(iso.Db.Notifications, n => n.UserId == driverId && n.Title == "Xác minh bằng lái không thành công");
        var (corrected, _) = await s.Verify.UpdateLicenseAsync(driverId, Licence("B") with { LicenseNumber = "790999888777" });
        Assert.Equal(DriverVerificationStatuses.Pending, corrected!.VerificationStatus);

        var (verified, verifyError) = await s.Verify.VerifyAsync(driverId, DispatcherId, true, null);
        Assert.True(verified is not null, verifyError);
        Assert.Equal(DriverVerificationStatuses.Verified, verified!.VerificationStatus);
        Assert.Equal("Trần Thị Điều Phối", verified.VerifiedByName);
        Assert.Contains(iso.Db.Notifications, n => n.UserId == driverId && n.Title == "Bạn đã được xác minh");

        var assigned = await s.Dispatch.AssignTripAsync(bookingId, new AssignTripRequest(driverId, 1), DispatcherId);
        Assert.True(assigned.Error is null, assigned.Error);
    }

    [Fact]
    public async Task D03_B_licence_drives_7_seats_but_not_the_16_seat_van()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        MakeAvailable(iso, 6);

        var van = await ConfirmedWithDriverAsync(iso, s, typeId: 3);
        var blocked = await s.Dispatch.AssignTripAsync(van, new AssignTripRequest(6, 5), DispatcherId);
        Assert.Equal("Bằng B2 (cũ) không lái được xe 16 chỗ (cần D1 trở lên).", blocked.Error);
        var (options, _) = await s.Dispatch.GetAssignableAsync(van);
        Assert.DoesNotContain(options!.Drivers, d => d.DriverId == 6);

        var suv = await ConfirmedWithDriverAsync(iso, s, typeId: 2);
        var ok = await s.Dispatch.AssignTripAsync(suv, new AssignTripRequest(6, 3), DispatcherId);
        Assert.True(ok.Error is null, ok.Error);
    }

    [Fact]
    public async Task D04_licence_expiring_before_return_blocks_assignment_and_customer_choice()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        MakeAvailable(iso, 6);
        iso.Db.Drivers.Find(6)!.LicenseExpiry = DateOnly.FromDateTime(Start.AddDays(-1));
        iso.Db.SaveChanges();

        var bookingId = await ConfirmedWithDriverAsync(iso, s, typeId: 1);
        Assert.Equal(DriverQualification.LicenceExpires,
            (await s.Dispatch.AssignTripAsync(bookingId, new AssignTripRequest(6, 1), DispatcherId)).Error);
        var eligibility = new DriverEligibilityService(iso.Db, new ScheduleConflictService(iso.Db));
        Assert.DoesNotContain(await eligibility.ListSelectableAsync(null, 1, Start, End), d => d.DriverId == 6);
    }

    [Fact]
    public async Task D06_backfill_sets_classes_verifies_sample_drivers_and_clears_placeholders()
    {
        using var iso = new IsolatedCarRentalDb();
        foreach (var t in iso.Db.VehicleTypes) t.MinLicenseClass = null;
        var sample = iso.Db.Drivers.Find(5)!;
        sample.LicenseClass = null;
        sample.VerificationStatus = DriverVerificationStatuses.Pending;
        var placeholderId = AddPendingDriver(iso, "DRV-000123", DateOnly.FromDateTime(DateTime.Today.AddYears(10)));
        iso.Db.SaveChanges();

        Assert.True(DriverLicenseBackfill.Run(iso.Db) > 0);
        Assert.Equal(0, DriverLicenseBackfill.Run(iso.Db)); // runs again without changing anything

        Assert.Equal("D1", iso.Db.VehicleTypes.Single(t => t.SeatCapacity == 16).MinLicenseClass);
        Assert.Equal("B", iso.Db.VehicleTypes.Single(t => t.SeatCapacity == 9).MinLicenseClass);
        Assert.Equal("B2", iso.Db.Drivers.Find(5)!.LicenseClass);
        Assert.Equal(DriverVerificationStatuses.Verified, iso.Db.Drivers.Find(5)!.VerificationStatus);
        var placeholder = iso.Db.Drivers.Find(placeholderId)!;
        Assert.Equal(string.Empty, placeholder.LicenseNumber);
        Assert.Equal(DriverVerificationStatuses.Pending, placeholder.VerificationStatus);
    }

    [Fact]
    public async Task Expiry_reminder_is_sent_once_per_expiry_date()
    {
        using var iso = new IsolatedCarRentalDb();
        iso.Db.Drivers.Find(6)!.LicenseExpiry = DateOnly.FromDateTime(DateTime.Today.AddDays(10));
        iso.Db.SaveChanges();

        Assert.Equal(1, await LicenseExpiryNotifier.RunOnceAsync(iso.Db, DateOnly.FromDateTime(DateTime.Today)));
        Assert.Equal(0, await LicenseExpiryNotifier.RunOnceAsync(iso.Db, DateOnly.FromDateTime(DateTime.Today)));
        Assert.Contains(iso.Db.Notifications, n => n.UserId == 6 && n.Title == LicenseExpiryNotifier.Title);
    }

    // ---------- helpers ----------

    private static UpdateDriverLicenseRequest Licence(string cls) => new(
        "790123456789", cls, DateOnly.FromDateTime(DateTime.Today.AddYears(-3)),
        DateOnly.FromDateTime(DateTime.Today.AddYears(5)), "079201000123");

    private static int AddPendingDriver(IsolatedCarRentalDb iso, string number = "", DateOnly? expiry = null)
    {
        var user = new User
        {
            Email = $"pending{Guid.NewGuid():N}@carrental.vn", PasswordHash = "x", FullName = "Tài Xế Chờ",
            Phone = "09" + Random.Shared.Next(10000000, 99999999), RoleId = 4, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        iso.Db.Users.Add(user);
        iso.Db.SaveChanges();
        iso.Db.Drivers.Add(new Driver
        {
            DriverId = user.UserId, LicenseNumber = number, LicenseExpiry = expiry ?? DateOnly.MinValue,
            Status = DriverStatuses.Available, IsActive = true, VerificationStatus = DriverVerificationStatuses.Pending
        });
        iso.Db.SaveChanges();
        return user.UserId;
    }

    private static void MakeAvailable(IsolatedCarRentalDb iso, int driverId)
    {
        iso.Db.Drivers.Find(driverId)!.Status = DriverStatuses.Available;
        foreach (var t in iso.Db.TripAssignments.Where(t => t.DriverId == driverId && t.Status != TripAssignmentStatuses.Completed))
            t.Status = TripAssignmentStatuses.Cancelled;
        iso.Db.SaveChanges();
    }

    private static async Task<int> ConfirmedWithDriverAsync(IsolatedCarRentalDb iso, Svc s, int typeId)
    {
        var created = await s.Bookings.CreateBookingAsync(3,
            new CreateBookingRequest(typeId, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.WithDriver));
        Assert.NotNull(created);
        var bookingId = created!.BookingId;
        iso.Db.Bookings.Find(bookingId)!.Status = BookingStatuses.Confirmed;
        iso.Db.SaveChanges();
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, bookingId);
        return bookingId;
    }

    private sealed record Svc(BookingService Bookings, DispatchService Dispatch, DriverVerificationService Verify, UserDocumentService Docs);

    private Svc Services(IsolatedCarRentalDb iso)
    {
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees, new ScheduleConflictService(iso.Db));
        var dispatch = new DispatchService(iso.Db, bookings, drivers, inspections, fees, new ScheduleConflictService(iso.Db));
        var docs = new UserDocumentService(iso.Db, new DocumentStorage(docRoot));
        var config = new ConfigurationBuilder().Build();
        return new Svc(bookings, dispatch, new DriverVerificationService(iso.Db, docs, config), docs);
    }

    private static HttpRequestMessage Authed(HttpMethod method, string url, string token, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
