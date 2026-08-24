using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

public class CarRentalDbContext(DbContextOptions<CarRentalDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<VehicleType> VehicleTypes => Set<VehicleType>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<TripAssignment> TripAssignments => Set<TripAssignment>();
    public DbSet<BookingStatusHistory> BookingStatusHistories => Set<BookingStatusHistory>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.Property(x => x.RoleName).HasMaxLength(50);
            e.Property(x => x.Description).HasMaxLength(200);
        });

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.Property(x => x.Email).HasMaxLength(100);
            e.Property(x => x.PasswordHash).HasMaxLength(256);
            e.Property(x => x.FullName).HasMaxLength(100);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.HasOne(x => x.Role).WithMany(x => x.Users).HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<Customer>(e =>
        {
            e.ToTable("Customers");
            e.HasKey(x => x.CustomerId);
            e.Property(x => x.Address).HasMaxLength(255);
            e.Property(x => x.IdNumber).HasMaxLength(20);
            e.HasOne(x => x.User).WithOne(x => x.Customer).HasForeignKey<Customer>(x => x.CustomerId);
        });

        modelBuilder.Entity<Driver>(e =>
        {
            e.ToTable("Drivers");
            e.HasKey(x => x.DriverId);
            e.Property(x => x.LicenseNumber).HasMaxLength(30);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.AverageRating).HasPrecision(3, 2);
            e.HasOne(x => x.User).WithOne(x => x.Driver).HasForeignKey<Driver>(x => x.DriverId);
        });

        modelBuilder.Entity<VehicleType>(e =>
        {
            e.ToTable("VehicleTypes");
            e.HasKey(x => x.TypeId);
            e.Property(x => x.TypeName).HasMaxLength(50);
            e.Property(x => x.PricePerDay).HasPrecision(18, 2);
            e.Property(x => x.PricePerKm).HasPrecision(18, 2);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.ImageUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<Vehicle>(e =>
        {
            e.ToTable("Vehicles");
            e.Property(x => x.LicensePlate).HasMaxLength(20);
            e.Property(x => x.Brand).HasMaxLength(50);
            e.Property(x => x.Model).HasMaxLength(50);
            e.Property(x => x.Color).HasMaxLength(30);
            e.Property(x => x.Status).HasMaxLength(20);
            e.HasOne(x => x.VehicleType).WithMany(x => x.Vehicles).HasForeignKey(x => x.TypeId);
        });

        modelBuilder.Entity<Booking>(e =>
        {
            e.ToTable("Bookings");
            e.Property(x => x.PickupAddress).HasMaxLength(255);
            e.Property(x => x.DropoffAddress).HasMaxLength(255);
            e.Property(x => x.PickupLat).HasPrecision(10, 7);
            e.Property(x => x.PickupLng).HasPrecision(10, 7);
            e.Property(x => x.DropoffLat).HasPrecision(10, 7);
            e.Property(x => x.DropoffLng).HasPrecision(10, 7);
            e.Property(x => x.EstimatedDistance).HasPrecision(10, 2);
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.Status).HasMaxLength(30);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasOne(x => x.Customer).WithMany(x => x.Bookings).HasForeignKey(x => x.CustomerId);
            e.HasOne(x => x.VehicleType).WithMany(x => x.Bookings).HasForeignKey(x => x.VehicleTypeId);
        });

        modelBuilder.Entity<TripAssignment>(e =>
        {
            e.ToTable("TripAssignments");
            e.HasKey(x => x.AssignmentId);
            e.HasIndex(x => x.BookingId).IsUnique();
            e.Property(x => x.Status).HasMaxLength(20);
            e.HasOne(x => x.Booking).WithOne(x => x.TripAssignment).HasForeignKey<TripAssignment>(x => x.BookingId);
            e.HasOne(x => x.Driver).WithMany(x => x.TripAssignments).HasForeignKey(x => x.DriverId);
            e.HasOne(x => x.Vehicle).WithMany(x => x.TripAssignments).HasForeignKey(x => x.VehicleId);
            e.HasOne(x => x.AssignedByUser).WithMany().HasForeignKey(x => x.AssignedBy);
        });

        modelBuilder.Entity<BookingStatusHistory>(e =>
        {
            e.ToTable("BookingStatusHistory");
            e.HasKey(x => x.HistoryId);
            e.Property(x => x.OldStatus).HasMaxLength(30);
            e.Property(x => x.NewStatus).HasMaxLength(30);
            e.Property(x => x.Note).HasMaxLength(300);
            e.HasOne(x => x.Booking).WithMany(x => x.StatusHistory).HasForeignKey(x => x.BookingId);
            e.HasOne(x => x.ChangedByUser).WithMany().HasForeignKey(x => x.ChangedBy);
        });

        modelBuilder.Entity<Payment>(e =>
        {
            e.ToTable("Payments");
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Method).HasMaxLength(30);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.TransactionRef).HasMaxLength(100);
            e.HasOne(x => x.Booking).WithMany(x => x.Payments).HasForeignKey(x => x.BookingId);
        });

        modelBuilder.Entity<Review>(e =>
        {
            e.ToTable("Reviews");
            e.HasIndex(x => x.BookingId).IsUnique();
            e.Property(x => x.Comment).HasMaxLength(500);
            e.HasOne(x => x.Booking).WithOne(x => x.Review).HasForeignKey<Review>(x => x.BookingId);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
            e.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId);
        });
    }
}
