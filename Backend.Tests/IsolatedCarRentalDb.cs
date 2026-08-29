using Backend.Constants;
using Backend.Data;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests;

internal sealed class IsolatedCarRentalDb : IDisposable
{
    public string Path { get; }
    public CarRentalDbContext Db { get; }

    public IsolatedCarRentalDb()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"crs-admin-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<CarRentalDbContext>()
            .UseSqlite($"Data Source={Path}")
            .Options;
        Db = new CarRentalDbContext(options);
        Db.Database.EnsureCreated();
        DbSeeder.Seed(Db);
        AlignLiveLikeBookings(Db);
    }

    /// <summary>
    /// Isolated copy of live #1/#2 invariants (not carrental.db).
    /// </summary>
    public static void AlignLiveLikeBookings(CarRentalDbContext db)
    {
        var b1 = db.Bookings.Find(1);
        if (b1 is null) return;
        b1.Status = BookingStatuses.Assigned;
        b1.AssignedVehicleId = 3;
        b1.FinalAmount = null;
        if (!db.TripAssignments.Any(t => t.BookingId == 1))
        {
            db.TripAssignments.Add(new TripAssignment
            {
                BookingId = 1,
                DriverId = 5,
                VehicleId = 3,
                AssignedBy = 2,
                AssignedAt = DateTime.UtcNow,
                Status = TripAssignmentStatuses.Assigned
            });
        }

        var b2 = db.Bookings.Find(2);
        if (b2 is not null)
            b2.FinalAmount = null;

        db.SaveChanges();
    }

    public Booking AddDepositBooking(decimal deposit)
    {
        var booking = new Booking
        {
            CustomerId = 3,
            VehicleTypeId = 1,
            PickupAddress = "A",
            DropoffAddress = "B",
            StartDate = new DateTime(2026, 10, 1, 8, 0, 0),
            EndDate = new DateTime(2026, 10, 2, 18, 0, 0),
            EstimatedDistance = 50,
            TotalAmount = 1_000_000,
            QuotedDepositAmount = deposit,
            Status = BookingStatuses.Pending,
            RentalMode = RentalModes.WithDriver,
            CreatedAt = DateTime.UtcNow
        };
        Db.Bookings.Add(booking);
        Db.SaveChanges();
        return booking;
    }

    public void Dispose()
    {
        Db.Dispose();
        TryDelete(Path);
        TryDelete(Path + "-shm");
        TryDelete(Path + "-wal");
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* temp file */ }
    }
}
