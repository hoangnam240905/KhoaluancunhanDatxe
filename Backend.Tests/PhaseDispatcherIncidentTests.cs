using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Constants;
using Backend.DTOs.Auth;
using Backend.DTOs.Bookings;
using Backend.DTOs.Incidents;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

/// <summary>Dispatcher records incidents reported by phone — self-drive (no driver) and with-driver trips.</summary>
public class PhaseDispatcherIncidentTests
{
    private const int DispatcherId = 2;
    private const int DriverId = 6;
    private const int CustomerId = 3;
    private static readonly DateTime Start = DateTime.Today.AddDays(80).AddHours(9);
    private static readonly DateTime End = Start.AddDays(1);

    [Fact]
    public async Task Self_drive_incident_has_no_driver_and_records_the_dispatcher()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var created = await s.Bookings.CreateBookingAsync(CustomerId,
            new CreateBookingRequest(1, "A", "A", null, null, null, null, Start, End, 20, null, RentalModes.SelfDrive));
        var booking = iso.Db.Bookings.Find(created!.BookingId)!;
        booking.Status = BookingStatuses.InProgress;
        booking.AssignedVehicleId = 2;
        iso.Db.SaveChanges();

        var (incident, error, status) = await s.Incidents.CreateByDispatcherAsync(DispatcherId, booking.BookingId,
            new CreateIncidentRequest(IncidentTypes.VehicleIssue, "  Khách báo nổ lốp trên cao tốc  "));
        Assert.True(incident is not null, error);
        Assert.Equal(201, status);
        Assert.Null(incident!.DriverId);
        Assert.Null(incident.AssignmentId);
        Assert.Equal("", incident.DriverName);
        Assert.Equal("Khách báo nổ lốp trên cao tốc", incident.Description);
        Assert.Equal("Trần Thị Điều Phối", incident.ReportedByName);
        Assert.Equal(RoleNames.Dispatcher, incident.ReportedByRole);

        var listed = Assert.Single(await s.Incidents.GetOperationsAsync(booking.BookingId, IncidentStatuses.Open));
        Assert.Equal("Trần Thị Điều Phối", listed.ReportedByName);
        // It can be resolved like any other incident.
        Assert.NotNull((await s.Incidents.ResolveAsync(listed.IncidentId, DispatcherId, new ResolveIncidentRequest("Đã gọi cứu hộ"))).Incident);
    }

    [Fact]
    public async Task With_driver_incident_keeps_the_trip_driver_and_notifies_them()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var created = await s.Bookings.CreateBookingAsync(CustomerId,
            new CreateBookingRequest(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.WithDriver));
        await s.Dispatch.ConfirmBookingAsync(created!.BookingId, DispatcherId);
        TestDispatchReady.EnsureSignedAndPaid(iso.Db, created.BookingId);
        Assert.Null((await s.Dispatch.AssignTripAsync(created.BookingId, new AssignTripRequest(DriverId, 2), DispatcherId)).Error);
        var assignmentId = iso.Db.TripAssignments.Single(t => t.BookingId == created.BookingId).AssignmentId;

        var (incident, error, _) = await s.Incidents.CreateByDispatcherAsync(DispatcherId, created.BookingId,
            new CreateIncidentRequest(IncidentTypes.CustomerIssue, "Khách đổi điểm đón phút chót"));
        Assert.True(incident is not null, error);
        Assert.Equal(DriverId, incident!.DriverId);
        Assert.Equal(assignmentId, incident.AssignmentId);
        Assert.Equal("Võ Thị Lái", incident.DriverName);
        Assert.Contains(iso.Db.Notifications, n => n.UserId == DriverId && n.Title == $"Điều phối ghi sự cố cho chuyến #{created.BookingId}");
        var mine = (await s.Incidents.GetByDriverAsync(DriverId)).Incidents!;
        Assert.Contains(mine, i => i.IncidentId == incident.IncidentId);

        // A driver's own report records the driver as reporter.
        var (own, ownError, _) = await s.Incidents.CreateAsync(DriverId, assignmentId,
            new CreateIncidentRequest(IncidentTypes.Other, "Kẹt xe nặng"));
        Assert.True(own is not null, ownError);
        Assert.Equal("Võ Thị Lái", own!.ReportedByName);
        Assert.Equal(RoleNames.Driver, own.ReportedByRole);
    }

    [Fact]
    public async Task Invalid_reports_are_refused()
    {
        using var iso = new IsolatedCarRentalDb();
        var s = Services(iso);
        var pending = await s.Bookings.CreateBookingAsync(CustomerId,
            new CreateBookingRequest(1, "A", "B", null, null, null, null, Start, End, 20, null, RentalModes.WithDriver));
        var id = pending!.BookingId;

        Assert.Equal(IncidentService.BookingNotRunning,
            (await s.Incidents.CreateByDispatcherAsync(DispatcherId, id, new CreateIncidentRequest(IncidentTypes.Other, "x"))).Error);
        Assert.Equal(404,
            (await s.Incidents.CreateByDispatcherAsync(DispatcherId, 99999, new CreateIncidentRequest(IncidentTypes.Other, "x"))).StatusCode);
        Assert.Equal(IncidentService.InvalidType,
            (await s.Incidents.CreateByDispatcherAsync(DispatcherId, id, new CreateIncidentRequest("Bão", "x"))).Error);
        Assert.Equal(IncidentService.DescriptionRequired,
            (await s.Incidents.CreateByDispatcherAsync(DispatcherId, id, new CreateIncidentRequest(IncidentTypes.Other, "  "))).Error);
        Assert.Equal(IncidentService.OccurredInFuture,
            (await s.Incidents.CreateByDispatcherAsync(DispatcherId, id,
                new CreateIncidentRequest(IncidentTypes.Other, "x", DateTime.UtcNow.AddHours(2)))).Error);
        Assert.Empty(iso.Db.IncidentReports);
    }

    [Fact]
    public void Old_incident_table_is_rebuilt_keeping_rows_and_reporter_is_backfilled()
    {
        using var iso = new IsolatedCarRentalDb();
        var db = iso.Db;
        db.Database.OpenConnection();
        db.Database.ExecuteSqlRaw("DROP TABLE \"IncidentReports\"");
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE "IncidentReports" (
                "IncidentId" INTEGER NOT NULL CONSTRAINT "PK_IncidentReports" PRIMARY KEY AUTOINCREMENT,
                "BookingId" INTEGER NOT NULL, "AssignmentId" INTEGER NOT NULL, "DriverId" INTEGER NOT NULL,
                "IncidentType" TEXT NOT NULL, "Description" TEXT NOT NULL, "OccurredAt" TEXT NOT NULL,
                "Status" TEXT NOT NULL, "CreatedAt" TEXT NOT NULL,
                "ResolvedByUserId" INTEGER NULL, "ResolvedByName" TEXT NULL, "ResolvedByRole" TEXT NULL,
                "ResolvedAt" TEXT NULL, "ResolutionNote" TEXT NULL)
            """);
        db.Database.ExecuteSqlRaw($"""
            INSERT INTO "IncidentReports" ("BookingId","AssignmentId","DriverId","IncidentType","Description","OccurredAt","Status","CreatedAt")
            VALUES (2, 1, {DriverId}, 'VehicleIssue', 'Xe kêu lạ', '2026-09-16 10:00:00', 'Open', '2026-09-16 10:00:00'),
                   (2, 1, {DriverId}, 'VehicleIssue', 'Điều phối X đối chiếu phiếu trả xe: có lệch.', '2026-09-17 10:00:00', 'Open', '2026-09-17 10:00:00')
            """);

        db.EnsureSqliteIncidentReportsOptionalDriver();
        db.EnsureSqliteIncidentReportsOptionalDriver(); // idempotent

        db.ChangeTracker.Clear();
        var rows = db.IncidentReports.OrderBy(i => i.IncidentId).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal("Xe kêu lạ", rows[0].Description);
        Assert.Equal("Võ Thị Lái", rows[0].ReportedByName);
        Assert.Equal("Driver", rows[0].ReportedByRole);
        Assert.Null(rows[1].ReportedByName);

        // DriverId / AssignmentId now accept null (self-drive incident).
        db.IncidentReports.Add(new Backend.Entities.IncidentReport
        {
            BookingId = 2, IncidentType = IncidentTypes.Other, Description = "Tự lái", OccurredAt = DateTime.UtcNow,
            Status = IncidentStatuses.Open, CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        Assert.Equal(3, db.IncidentReports.Count());
        Assert.True(db.IncidentReports.Max(i => i.IncidentId) > rows[1].IncidentId);
    }

    [Fact]
    public void Old_reviews_table_is_rebuilt_so_self_drive_reviews_fit_keeping_rows_and_unique_index()
    {
        using var iso = new IsolatedCarRentalDb();
        var db = iso.Db;
        db.Database.OpenConnection();
        db.Database.ExecuteSqlRaw("DROP TABLE \"Reviews\"");
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE "Reviews" (
                "ReviewId" INTEGER NOT NULL CONSTRAINT "PK_Reviews" PRIMARY KEY AUTOINCREMENT,
                "BookingId" INTEGER NOT NULL, "CustomerId" INTEGER NOT NULL, "DriverId" INTEGER NOT NULL,
                "Rating" INTEGER NOT NULL, "Comment" TEXT NULL, "CreatedAt" TEXT NOT NULL,
                "ComfortRating" INTEGER NULL, "CleanlinessRating" INTEGER NULL, "PerformanceRating" INTEGER NULL, "ValueRating" INTEGER NULL)
            """);
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX \"IX_Reviews_BookingId\" ON \"Reviews\" (\"BookingId\")");
        db.Database.ExecuteSqlRaw($"""
            INSERT INTO "Reviews" ("BookingId","CustomerId","DriverId","Rating","Comment","CreatedAt")
            VALUES (2, 4, {DriverId}, 4, 'Tốt', '2026-09-20 10:00:00')
            """);

        db.EnsureSqliteReviewsOptionalDriver();
        db.EnsureSqliteReviewsOptionalDriver(); // idempotent

        db.ChangeTracker.Clear();
        var old = Assert.Single(db.Reviews);
        Assert.Equal(DriverId, old.DriverId);
        Assert.Equal("Tốt", old.Comment);
        // A review without a driver now fits; the unique index on BookingId is still there.
        db.Reviews.Add(new Backend.Entities.Review { BookingId = 1, CustomerId = 3, Rating = 5, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        db.Reviews.Add(new Backend.Entities.Review { BookingId = 1, CustomerId = 3, Rating = 3, CreatedAt = DateTime.UtcNow });
        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public async Task Api_only_dispatcher_can_record_incidents()
    {
        using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var customer = await LoginAsync(client, "customer1@gmail.com");
        var driver = await LoginAsync(client, "driver2@carrental.vn");
        foreach (var token in new[] { customer, driver })
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/dispatch/bookings/2/incidents")
            {
                Content = JsonContent.Create(new CreateIncidentRequest(IncidentTypes.Other, "x"))
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(req)).StatusCode);
        }
    }

    private sealed record Svc(BookingService Bookings, DispatchService Dispatch, IncidentService Incidents);

    private static Svc Services(IsolatedCarRentalDb iso)
    {
        var pricing = new PricingService();
        var bookings = new BookingService(iso.Db, pricing);
        var inspections = new VehicleInspectionService(iso.Db);
        var fees = new BookingFeeService(iso.Db, pricing);
        var drivers = new DriverService(iso.Db, bookings, inspections, fees, new ScheduleConflictService(iso.Db), null);
        var dispatch = new DispatchService(iso.Db, bookings, drivers, inspections, fees, new ScheduleConflictService(iso.Db), null);
        return new Svc(bookings, dispatch, new IncidentService(iso.Db));
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
    }
}
