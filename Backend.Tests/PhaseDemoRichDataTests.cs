using Backend.Constants;
using Backend.Data;
using Backend.Entities;
using Backend.Services;
using Backend.Validation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class PhaseDemoRichDataTests
{
    [Fact]
    public void Core_seed_without_demo_rich_keeps_invariants()
    {
        using var iso = new IsolatedCarRentalDb();
        Assert.Equal(4, iso.Db.VehicleTypes.Count());
        Assert.Equal(6, iso.Db.Vehicles.Count());
        Assert.Equal(3, iso.Db.Drivers.Count());
        Assert.Equal(2, iso.Db.Customers.Count());
        Assert.Equal(2, iso.Db.Bookings.Count());
        Assert.False(iso.Db.VehicleTypes.Any(t => t.TypeName == DemoRichSeeder.BusTypeName));
        Assert.Equal(6_600_000m, iso.Db.Bookings.Find(1)!.TotalAmount);
        Assert.Equal(2_240_000m, iso.Db.Bookings.Find(2)!.TotalAmount);
        Assert.Equal(2, iso.Db.Payments.Single(p => p.PaymentId == 1).BookingId);
        Assert.Null(iso.Db.Payments.Find(1)!.PaymentType);
    }

    [Fact]
    public void Demo_rich_is_idempotent_and_appends_after_core()
    {
        using var iso = new IsolatedCarRentalDb();
        DemoRichSeeder.Seed(iso.Db);
        var types = iso.Db.VehicleTypes.Count();
        var vehicles = iso.Db.Vehicles.Count();
        DemoRichSeeder.Seed(iso.Db);
        var b1 = iso.Db.Bookings.Include(b => b.TripAssignment).Single(b => b.BookingId == 1);
        Assert.Equal(types, iso.Db.VehicleTypes.Count());
        Assert.Equal(vehicles, iso.Db.Vehicles.Count());
        Assert.Equal(6_600_000m, b1.TotalAmount);
        Assert.Equal(BookingStatuses.Assigned, b1.Status);
        Assert.Equal(5, b1.TripAssignment!.DriverId);
        Assert.Equal(3, b1.AssignedVehicleId);
        Assert.Equal(2_240_000m, iso.Db.Bookings.Find(2)!.TotalAmount);
        Assert.Null(iso.Db.Payments.Find(1)!.PaymentType);
    }

    [Fact]
    public void Demo_rich_has_expected_catalog_and_lifecycle()
    {
        using var iso = new IsolatedCarRentalDb();
        DemoRichSeeder.Seed(iso.Db);
        iso.Db.ReconcileOpenAssignmentResourceStatus();

        Assert.Equal(6, iso.Db.VehicleTypes.Count());
        var bus = iso.Db.VehicleTypes.Single(t => t.TypeName == DemoRichSeeder.BusTypeName);
        Assert.Equal(29, bus.SeatCapacity);
        Assert.True(bus.PricePerDay > iso.Db.VehicleTypes.Single(t => t.TypeId == 2).PricePerDay);
        Assert.NotNull(iso.Db.VehicleTypes.Single(t => t.TypeName == DemoRichSeeder.MpvTypeName));

        Assert.InRange(iso.Db.Vehicles.Count(), 22, 26);
        Assert.InRange(iso.Db.Vehicles.Count(v => v.Status == VehicleStatuses.Available), 10, 18);
        Assert.InRange(iso.Db.Vehicles.Count(v => v.Status == VehicleStatuses.Rented), 3, 6);
        Assert.Equal(3, iso.Db.Vehicles.Count(v => v.Status == VehicleStatuses.Maintenance));
        Assert.Equal(3, iso.Db.Vehicles.Count(v => v.Status == VehicleStatuses.Inactive));
        Assert.Equal(3, iso.Db.Vehicles.Count(v => v.TypeId == bus.TypeId));

        Assert.InRange(iso.Db.Drivers.Count(), 11, 13);
        Assert.InRange(iso.Db.Customers.Count(), 19, 21);
        Assert.InRange(iso.Db.Bookings.Count(), 55, 70);
        Assert.InRange(iso.Db.Bookings.Count(b => b.Status == BookingStatuses.Pending), 6, 10);
        Assert.InRange(iso.Db.Bookings.Count(b => b.Status == BookingStatuses.Confirmed), 5, 8);
        Assert.InRange(iso.Db.Bookings.Count(b => b.Status == BookingStatuses.Assigned), 6, 10);
        Assert.InRange(iso.Db.Bookings.Count(b => b.Status == BookingStatuses.InProgress), 2, 5);
        Assert.InRange(iso.Db.Bookings.Count(b => b.Status == BookingStatuses.Completed), 20, 30);
        Assert.InRange(iso.Db.Bookings.Count(b => b.Status == BookingStatuses.Cancelled), 8, 12);
        Assert.InRange(iso.Db.Bookings.Count(b => b.RentalMode == RentalModes.SelfDrive), 12, 24);
        Assert.InRange(iso.Db.Bookings.Count(b => b.SourceRecommended), 12, 25);
        Assert.True(iso.Db.Bookings.Count(b => b.SourceRecommended && b.Status == BookingStatuses.Completed) >= 6);

        Assert.True(iso.Db.Bookings.Any(b =>
            b.VehicleTypeId == bus.TypeId && b.RentalMode == RentalModes.WithDriver
            && b.Status == BookingStatuses.Completed));
        Assert.True(iso.Db.Bookings.Any(b =>
            b.VehicleTypeId == bus.TypeId && b.Status == BookingStatuses.Assigned));
        Assert.True(iso.Db.Bookings.Any(b =>
            b.VehicleTypeId == bus.TypeId && b.Status == BookingStatuses.Pending));

        AssertDemoDriversHaveAtMostOneOpen(iso.Db, ignoreCoreDriver5: true);
    }

    [Fact]
    public void Demo_rich_create_database_has_at_most_one_open_assignment_per_driver()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"crs-demo-open-{Guid.NewGuid():N}.db");
        try
        {
            DemoRichSeeder.CreateDemoDatabase(path);
            var options = new DbContextOptionsBuilder<CarRentalDbContext>()
                .UseSqlite($"Data Source={path}")
                .Options;
            using var db = new CarRentalDbContext(options);
            AssertDemoDriversHaveAtMostOneOpen(db, ignoreCoreDriver5: false);

            var driver4 = db.Users.Single(u => u.Email == "driver4@carrental.vn").UserId;
            Assert.True(db.TripAssignments.Count(t =>
                t.DriverId == driver4 && t.Status == TripAssignmentStatuses.Cancelled) >= 2);
            Assert.True(db.TripAssignments.Count(t =>
                t.DriverId == driver4 && t.Status == TripAssignmentStatuses.Completed) >= 1);
            Assert.True(db.TripAssignments.Count(t => t.Status == TripAssignmentStatuses.Completed) >= 15);
            Assert.Contains(db.Bookings, b =>
                b.VehicleTypeId == db.VehicleTypes.Single(t => t.TypeName == DemoRichSeeder.BusTypeName).TypeId
                && b.Status == BookingStatuses.Assigned);
        }
        finally
        {
            try { System.IO.File.Delete(path); } catch { /* temp */ }
            try { System.IO.File.Delete(path + "-shm"); } catch { /* temp */ }
            try { System.IO.File.Delete(path + "-wal"); } catch { /* temp */ }
        }
    }

    private static void AssertDemoDriversHaveAtMostOneOpen(CarRentalDbContext db, bool ignoreCoreDriver5)
    {
        var open = new[]
        {
            TripAssignmentStatuses.Assigned,
            TripAssignmentStatuses.Accepted,
            TripAssignmentStatuses.InProgress
        };
        var query = db.TripAssignments.Where(t => open.Contains(t.Status));
        if (ignoreCoreDriver5)
            query = query.Where(t => t.BookingId > 2);

        var duplicates = query
            .GroupBy(t => t.DriverId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        Assert.True(duplicates.Count == 0,
            $"Drivers with >1 open assignment: {string.Join(", ", duplicates)}");
    }

    [Fact]
    public async Task Demo_rich_reviews_and_maintenance_and_dashboard()
    {
        using var iso = new IsolatedCarRentalDb();
        DemoRichSeeder.Seed(iso.Db);
        iso.Db.ReconcileOpenAssignmentResourceStatus();

        Assert.Equal(12, iso.Db.Reviews.Count());
        Assert.All(iso.Db.Reviews, r =>
        {
            var booking = iso.Db.Bookings.Single(b => b.BookingId == r.BookingId);
            Assert.Equal(BookingStatuses.Completed, booking.Status);
            Assert.Equal(RentalModes.WithDriver, booking.RentalMode);
            Assert.Equal(booking.CustomerId, r.CustomerId);
            var assignment = iso.Db.TripAssignments.Single(t => t.BookingId == r.BookingId);
            Assert.Equal(assignment.DriverId, r.DriverId);
            Assert.Null(ReviewRules.ValidateComment(r.Rating, r.Comment, out _));
        });
        Assert.True(iso.Db.Bookings.Count(b =>
            b.Status == BookingStatuses.Completed
            && b.RentalMode == RentalModes.WithDriver
            && !iso.Db.Reviews.Any(r => r.BookingId == b.BookingId)) >= 2);

        Assert.InRange(iso.Db.MaintenanceRecords.Count(), 26, 40);
        var alerts = await new MaintenanceAlertService(iso.Db).GetAlertsAsync();
        Assert.Contains(alerts, a => a.Reason.Contains("kilomet", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(alerts, a => a.Reason.Contains("thời gian", StringComparison.OrdinalIgnoreCase)
                                     || a.Reason.Contains("Chưa có lịch sử", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(alerts, a => a.Reason.Contains("Chưa có lịch sử", StringComparison.OrdinalIgnoreCase));

        Assert.InRange(iso.Db.Contracts.Count(), 18, 26);
        Assert.Contains(iso.Db.Contracts, c => c.Status == ContractStatuses.Issued);
        Assert.Contains(iso.Db.Contracts, c => c.Status == ContractStatuses.Signed);
        Assert.Contains(iso.Db.Contracts, c => c.Status == ContractStatuses.Voided);
        Assert.Equal(iso.Db.Contracts.Count(), iso.Db.Contracts.Select(c => c.BookingId).Distinct().Count());

        Assert.InRange(iso.Db.Payments.Count(), 30, 50);
        Assert.Contains(iso.Db.Payments, p => p.Status == PaymentStatuses.Paid && p.PaymentType == PaymentTypes.Deposit);
        Assert.Contains(iso.Db.Payments, p => p.Status == PaymentStatuses.Pending);
        Assert.Contains(iso.Db.Payments, p => p.Status == PaymentStatuses.Failed);
        Assert.DoesNotContain(iso.Db.Payments, p => p.PaymentType == PaymentTypes.Balance);
        Assert.DoesNotContain(iso.Db.Payments, p => p.PaymentType == PaymentTypes.Refund);

        Assert.Equal(8, iso.Db.IncidentReports.Count());
        Assert.All(iso.Db.IncidentReports, i =>
        {
            Assert.Equal(IncidentStatuses.Open, i.Status);
            var a = iso.Db.TripAssignments.Single(t => t.AssignmentId == i.AssignmentId);
            Assert.Equal(a.DriverId, i.DriverId);
            Assert.Equal(a.BookingId, i.BookingId);
        });

        Assert.InRange(iso.Db.VehicleInspections.Count(), 24, 32);
        Assert.Contains(iso.Db.VehicleInspections, i => i.InspectionType == VehicleInspectionTypes.Handover);
        Assert.Contains(iso.Db.VehicleInspections, i => i.InspectionType == VehicleInspectionTypes.Return);

        var legalCoverage = iso.Db.Vehicles.Count(v => v.RegistrationNumber != null);
        Assert.True(legalCoverage >= 18);
        Assert.True(iso.Db.Vehicles.Count(v => v.RegistrationNumber == null) >= 2);

        var from = DateTime.UtcNow.AddDays(-30);
        var to = DateTime.UtcNow.AddDays(1);
        var (dashboard, error) = await new DashboardService(iso.Db, new MaintenanceAlertService(iso.Db))
            .GetAsync(from, to);
        Assert.Null(error);
        Assert.NotNull(dashboard);
        Assert.True(dashboard!.Summary.TotalBookings >= 40);
        Assert.True(dashboard.Summary.CompletedBookings >= 15);
        Assert.True(dashboard.Payments.PaidDepositCount >= 1);
        Assert.True(dashboard.Maintenance.AlertCount >= 3);
        Assert.NotEmpty(dashboard.TopVehicles);
        Assert.NotEmpty(dashboard.Drivers.Performance);
        Assert.True(dashboard.Recommendation.RecommendedBookings >= 6);
        Assert.Contains(dashboard.Drivers.Performance, d => d.CompletedAssignments > 0);
        Assert.Contains(dashboard.Drivers.Performance, d => d.IncidentCount > 0);
        Assert.Contains(dashboard.Drivers.Performance, d => d.AverageReviewRating is not null);
    }

    [Fact]
    public async Task Demo_rich_has_no_default_schedule_conflict_and_recommendable_types()
    {
        using var iso = new IsolatedCarRentalDb();
        DemoRichSeeder.Seed(iso.Db);
        iso.Db.ReconcileOpenAssignmentResourceStatus();
        var schedule = new ScheduleConflictService(iso.Db);

        var occupying = iso.Db.Bookings
            .Include(b => b.TripAssignment)
            .AsEnumerable()
            .Where(b => ScheduleConflictService.OccupiesSchedule(b.Status)
                        || (b.Status == BookingStatuses.Pending && b.AssignedVehicleId != null))
            .ToList();

        foreach (var booking in occupying)
        {
            if (booking.TripAssignment is { } trip)
            {
                Assert.False(
                    await schedule.HasVehicleConflictAsync(
                        trip.VehicleId, booking.StartDate, booking.EndDate, booking.BookingId),
                    $"Vehicle conflict on booking {booking.BookingId}");
                Assert.False(
                    await schedule.HasDriverConflictAsync(
                        trip.DriverId, booking.StartDate, booking.EndDate, booking.BookingId),
                    $"Driver conflict on booking {booking.BookingId}");
            }
            else if (booking.AssignedVehicleId is int vid)
            {
                Assert.False(
                    await schedule.HasVehicleConflictAsync(vid, booking.StartDate, booking.EndDate, booking.BookingId),
                    $"SelfDrive vehicle conflict on booking {booking.BookingId}");
            }
        }

        var start = DateTime.UtcNow.AddDays(45);
        var end = start.AddDays(1);
        var rec = new RecommendationService(iso.Db, schedule, new TestSnapshotRecommenderClient());
        var (items, recError, status) = await rec.RecommendAsync(start, end, null, null, null);
        Assert.Null(recError);
        Assert.Equal(200, status);
        Assert.NotNull(items);
        Assert.Contains(items!, x => x.VehicleTypeId == 1 && x.AvailableCount >= 1);
        Assert.Contains(items!, x => x.TypeName == DemoRichSeeder.BusTypeName && x.AvailableCount >= 1);
        Assert.Contains(items!, x => x.AvgRating > 0);
        Assert.True(items!.Select(x => x.Score).Distinct().Count() >= 2);
    }
}
