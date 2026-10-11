using Backend.Constants;
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
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<VehicleInspection> VehicleInspections => Set<VehicleInspection>();
    public DbSet<BookingFee> BookingFees => Set<BookingFee>();
    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();
    public DbSet<IncidentReport> IncidentReports => Set<IncidentReport>();
    public DbSet<EmailOtp> EmailOtps => Set<EmailOtp>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<VehicleStatusHistory> VehicleStatusHistories => Set<VehicleStatusHistory>();
    public DbSet<RecommendationLog> RecommendationLogs => Set<RecommendationLog>();
    public DbSet<VehicleImage> VehicleImages => Set<VehicleImage>();
    public DbSet<CustomerVehicleInteraction> CustomerVehicleInteractions => Set<CustomerVehicleInteraction>();

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
            e.Property(x => x.IsEmailVerified).HasDefaultValue(true);
            e.Property(x => x.LockReason).HasMaxLength(255);
            e.Property(x => x.InactiveReason).HasMaxLength(255);
            e.Property(x => x.GoogleSubject).HasMaxLength(128);
            e.HasIndex(x => x.GoogleSubject);
            e.HasOne(x => x.Role).WithMany(x => x.Users).HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<EmailOtp>(e =>
        {
            e.ToTable("EmailOtps");
            e.Property(x => x.Email).HasMaxLength(100);
            e.Property(x => x.Purpose).HasMaxLength(40);
            e.Property(x => x.CodeHash).HasMaxLength(128);
            e.HasIndex(x => new { x.Email, x.Purpose, x.CreatedAt });
        });

        modelBuilder.Entity<Customer>(e =>
        {
            e.ToTable("Customers");
            e.HasKey(x => x.CustomerId);
            e.Property(x => x.Address).HasMaxLength(255);
            e.Property(x => x.IdNumber).HasMaxLength(20);
            e.Property(x => x.VerificationStatus).HasDefaultValue("Pending");
            e.Property(x => x.MembershipTier).HasDefaultValue("Standard");
            e.HasIndex(x => x.VerificationStatus);
            e.HasIndex(x => x.MembershipTier);
            e.HasOne(x => x.User).WithOne(x => x.Customer).HasForeignKey<Customer>(x => x.CustomerId);
        });

        modelBuilder.Entity<Driver>(e =>
        {
            e.ToTable("Drivers");
            e.HasKey(x => x.DriverId);
            e.Property(x => x.LicenseNumber).HasMaxLength(30);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.AverageRating).HasPrecision(3, 2);
            e.Property(x => x.VerificationStatus).HasDefaultValue("Pending");
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.IsActive);
            e.HasOne(x => x.User).WithOne(x => x.Driver).HasForeignKey<Driver>(x => x.DriverId);
        });

        modelBuilder.Entity<VehicleType>(e =>
        {
            e.ToTable("VehicleTypes");
            e.HasKey(x => x.TypeId);
            e.Property(x => x.TypeName).HasMaxLength(50);
            e.Property(x => x.PricePerDay).HasPrecision(18, 2);
            e.Property(x => x.PricePerKm).HasPrecision(18, 2);
            e.Property(x => x.DriverFeePerDay).HasPrecision(18, 2);
            e.Property(x => x.SelfDrivePricePerDay).HasPrecision(18, 2);
            e.Property(x => x.SelfDriveIncludedKmPerDay).HasPrecision(10, 2);
            e.Property(x => x.SelfDriveExtraKmPrice).HasPrecision(18, 2);
            e.Property(x => x.WithDriverDepositAmount).HasPrecision(18, 2);
            e.Property(x => x.SelfDriveDepositAmount).HasPrecision(18, 2);
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
            e.Property(x => x.RegistrationNumber).HasMaxLength(30);
            e.Property(x => x.ConditionStatus).HasDefaultValue("Good");
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.FuelType);
            e.HasIndex(x => x.Transmission);
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
            e.Property(x => x.QuotedPricePerDay).HasPrecision(18, 2);
            e.Property(x => x.QuotedPricePerKm).HasPrecision(18, 2);
            e.Property(x => x.QuotedDriverFeePerDay).HasPrecision(18, 2);
            e.Property(x => x.QuotedSelfDriveIncludedKmPerDay).HasPrecision(10, 2);
            e.Property(x => x.QuotedSelfDriveExtraKmPrice).HasPrecision(18, 2);
            e.Property(x => x.QuotedDepositAmount).HasPrecision(18, 2);
            e.Property(x => x.FinalAmount).HasPrecision(18, 2);
            e.Property(x => x.Status).HasMaxLength(30);
            e.Property(x => x.RentalMode).HasMaxLength(20);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasOne(x => x.Customer).WithMany(x => x.Bookings).HasForeignKey(x => x.CustomerId);
            e.HasOne(x => x.VehicleType).WithMany(x => x.Bookings).HasForeignKey(x => x.VehicleTypeId);
            e.HasOne(x => x.AssignedVehicle)
                .WithMany()
                .HasForeignKey(x => x.AssignedVehicleId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
            e.HasOne(x => x.RequestedDriver)
                .WithMany()
                .HasForeignKey(x => x.RequestedDriverId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
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
            e.Property(x => x.PaymentType).HasMaxLength(30);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Method).HasMaxLength(30);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.TransactionRef).HasMaxLength(100);
            e.HasOne(x => x.Booking).WithMany(x => x.Payments).HasForeignKey(x => x.BookingId);
        });

        modelBuilder.Entity<Contract>(e =>
        {
            e.ToTable("Contracts");
            e.HasKey(x => x.ContractId);
            e.HasIndex(x => x.BookingId).IsUnique();
            e.HasIndex(x => x.ContractNumber).IsUnique();
            e.Property(x => x.ContractNumber).HasMaxLength(30);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.CustomerName).HasMaxLength(100);
            e.Property(x => x.VehicleTypeName).HasMaxLength(50);
            e.Property(x => x.RentalMode).HasMaxLength(20);
            e.Property(x => x.PickupAddress).HasMaxLength(255);
            e.Property(x => x.DropoffAddress).HasMaxLength(255);
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.DepositAmount).HasPrecision(18, 2);
            e.HasOne(x => x.Booking).WithOne(x => x.Contract).HasForeignKey<Contract>(x => x.BookingId);
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

        modelBuilder.Entity<VehicleInspection>(e =>
        {
            e.ToTable("VehicleInspections");
            e.HasKey(x => x.InspectionId);
            e.Property(x => x.InspectionType).HasMaxLength(20);
            e.Property(x => x.OdometerKm).HasPrecision(10, 2);
            e.Property(x => x.FuelLevel).HasPrecision(5, 2);
            e.Property(x => x.Condition).HasMaxLength(100);
            e.Property(x => x.ExteriorCondition).HasMaxLength(100);
            e.Property(x => x.TechnicalCondition).HasMaxLength(100);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasIndex(x => x.BookingId);
            e.HasIndex(x => x.VehicleId);
            e.HasOne(x => x.Booking).WithMany(x => x.Inspections).HasForeignKey(x => x.BookingId);
            e.HasOne(x => x.Vehicle)
                .WithMany(x => x.Inspections)
                .HasForeignKey(x => x.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BookingFee>(e =>
        {
            e.ToTable("BookingFees");
            e.HasKey(x => x.FeeId);
            e.Property(x => x.FeeType).HasMaxLength(30);
            e.Property(x => x.Description).HasMaxLength(300);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasIndex(x => x.BookingId);
            e.HasOne(x => x.Booking).WithMany(x => x.Fees).HasForeignKey(x => x.BookingId);
        });

        modelBuilder.Entity<MaintenanceRecord>(e =>
        {
            e.ToTable("MaintenanceRecords");
            e.HasKey(x => x.MaintenanceId);
            e.Property(x => x.MaintenanceType).HasMaxLength(20);
            e.Property(x => x.Cost).HasPrecision(18, 2);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasIndex(x => x.VehicleId);
            e.HasOne(x => x.Vehicle)
                .WithMany(x => x.MaintenanceRecords)
                .HasForeignKey(x => x.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IncidentReport>(e =>
        {
            e.ToTable("IncidentReports");
            e.HasKey(x => x.IncidentId);
            e.Property(x => x.IncidentType).HasMaxLength(30);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.Status).HasMaxLength(20);
            e.HasIndex(x => x.BookingId);
            e.HasIndex(x => x.DriverId);
            e.HasIndex(x => x.AssignmentId);
            e.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId);
            e.HasOne(x => x.Assignment).WithMany().HasForeignKey(x => x.AssignmentId);
            e.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId);
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.ToTable("Notifications");
            e.HasKey(x => x.NotificationId);
            e.Property(x => x.IsRead).HasDefaultValue(false);
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<VehicleStatusHistory>(e =>
        {
            e.ToTable("VehicleStatusHistory");
            e.HasKey(x => x.StatusHistoryId);
            e.HasIndex(x => x.VehicleId);
            e.HasOne(x => x.Vehicle)
                .WithMany(x => x.StatusHistory)
                .HasForeignKey(x => x.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ChangedByUser)
                .WithMany()
                .HasForeignKey(x => x.ChangedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        });

        modelBuilder.Entity<RecommendationLog>(e =>
        {
            e.ToTable("RecommendationLogs");
            e.HasKey(x => x.RecommendationId);
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.VehicleId);
            e.HasOne(x => x.Customer)
                .WithMany(x => x.RecommendationLogs)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Vehicle)
                .WithMany(x => x.RecommendationLogs)
                .HasForeignKey(x => x.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<VehicleImage>(e =>
        {
            e.ToTable("VehicleImages");
            e.HasKey(x => x.VehicleImageId);
            e.Property(x => x.IsPrimary).HasDefaultValue(false);
            e.Property(x => x.DisplayOrder).HasDefaultValue(0);
            e.HasIndex(x => x.VehicleId);
            e.HasOne(x => x.Vehicle)
                .WithMany(x => x.Images)
                .HasForeignKey(x => x.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CustomerVehicleInteraction>(e =>
        {
            e.ToTable("CustomerVehicleInteractions");
            e.HasKey(x => x.InteractionId);
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.VehicleId);
            e.HasIndex(x => new { x.InteractionType, x.CreatedAt });
            e.HasOne(x => x.Customer)
                .WithMany(x => x.VehicleInteractions)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Vehicle)
                .WithMany(x => x.VehicleInteractions)
                .HasForeignKey(x => x.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    /// <summary>
    /// Adds RentalMode / AssignedVehicleId to an existing SQLite database.
    /// EnsureCreated does not alter already-created tables.
    /// </summary>
    public void EnsureSqliteBookingRentalColumns()
    {
        if (Database.ProviderName is null ||
            !Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return;

        Database.OpenConnection();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('Bookings')";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                columns.Add(reader.GetString(1));
        }

        if (!columns.Contains("RentalMode"))
            Database.ExecuteSqlRaw(
                "ALTER TABLE Bookings ADD COLUMN RentalMode TEXT NOT NULL DEFAULT 'WithDriver'");

        if (!columns.Contains("AssignedVehicleId"))
            Database.ExecuteSqlRaw(
                "ALTER TABLE Bookings ADD COLUMN AssignedVehicleId INTEGER NULL REFERENCES Vehicles(VehicleId)");
    }

    /// <summary>
    /// Adds price-snapshot columns to an existing SQLite Bookings table.
    /// EnsureCreated does not alter already-created tables. Existing rows stay NULL.
    /// </summary>
    public void EnsureSqliteBookingPriceSnapshotColumns()
    {
        if (Database.ProviderName is null ||
            !Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return;

        Database.OpenConnection();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('Bookings')";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                columns.Add(reader.GetString(1));
        }

        if (!columns.Contains("QuotedPricePerDay"))
            Database.ExecuteSqlRaw(
                "ALTER TABLE Bookings ADD COLUMN QuotedPricePerDay TEXT NULL");

        if (!columns.Contains("QuotedPricePerKm"))
            Database.ExecuteSqlRaw(
                "ALTER TABLE Bookings ADD COLUMN QuotedPricePerKm TEXT NULL");

        if (!columns.Contains("QuotedDays"))
            Database.ExecuteSqlRaw(
                "ALTER TABLE Bookings ADD COLUMN QuotedDays INTEGER NULL");
    }

    /// <summary>
    /// Adds mode-specific rates to VehicleTypes. Existing rows stay NULL until FillVehicleTypePricingDefaults.
    /// </summary>
    public void EnsureSqliteVehicleTypePricingColumns()
    {
        if (Database.ProviderName is null ||
            !Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return;

        Database.OpenConnection();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('VehicleTypes')";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                columns.Add(reader.GetString(1));
        }

        void AddIfMissing(string name, string sql)
        {
            if (!columns.Contains(name))
                Database.ExecuteSqlRaw(sql);
        }

        AddIfMissing("DriverFeePerDay", "ALTER TABLE VehicleTypes ADD COLUMN DriverFeePerDay TEXT NULL");
        AddIfMissing("SelfDrivePricePerDay", "ALTER TABLE VehicleTypes ADD COLUMN SelfDrivePricePerDay TEXT NULL");
        AddIfMissing("SelfDriveIncludedKmPerDay", "ALTER TABLE VehicleTypes ADD COLUMN SelfDriveIncludedKmPerDay TEXT NULL");
        AddIfMissing("SelfDriveExtraKmPrice", "ALTER TABLE VehicleTypes ADD COLUMN SelfDriveExtraKmPrice TEXT NULL");
        AddIfMissing("WithDriverDepositAmount", "ALTER TABLE VehicleTypes ADD COLUMN WithDriverDepositAmount TEXT NULL");
        AddIfMissing("SelfDriveDepositAmount", "ALTER TABLE VehicleTypes ADD COLUMN SelfDriveDepositAmount TEXT NULL");
    }

    /// <summary>
    /// Adds mode snapshot columns on Bookings. Existing rows stay NULL (no backfill).
    /// </summary>
    public void EnsureSqliteBookingModeSnapshotColumns()
    {
        if (Database.ProviderName is null ||
            !Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return;

        Database.OpenConnection();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('Bookings')";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                columns.Add(reader.GetString(1));
        }

        if (!columns.Contains("QuotedDriverFeePerDay"))
            Database.ExecuteSqlRaw("ALTER TABLE Bookings ADD COLUMN QuotedDriverFeePerDay TEXT NULL");
        if (!columns.Contains("QuotedSelfDriveIncludedKmPerDay"))
            Database.ExecuteSqlRaw("ALTER TABLE Bookings ADD COLUMN QuotedSelfDriveIncludedKmPerDay TEXT NULL");
        if (!columns.Contains("QuotedSelfDriveExtraKmPrice"))
            Database.ExecuteSqlRaw("ALTER TABLE Bookings ADD COLUMN QuotedSelfDriveExtraKmPrice TEXT NULL");
        if (!columns.Contains("QuotedDepositAmount"))
            Database.ExecuteSqlRaw("ALTER TABLE Bookings ADD COLUMN QuotedDepositAmount TEXT NULL");
    }

    /// <summary>
    /// Adds PaymentType to existing SQLite Payments. Existing rows stay NULL (legacy seed).
    /// </summary>
    public void EnsureSqlitePaymentTypeColumn()
    {
        if (Database.ProviderName is null ||
            !Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return;

        Database.OpenConnection();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('Payments')";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                columns.Add(reader.GetString(1));
        }

        if (!columns.Contains("PaymentType"))
            Database.ExecuteSqlRaw("ALTER TABLE Payments ADD COLUMN PaymentType TEXT NULL");
    }

    /// <summary>
    /// Creates VehicleInspections on an existing SQLite database.
    /// EnsureCreated does not add new tables to a live DB. No backfill.
    /// </summary>
    public void EnsureSqliteVehicleInspectionsTable()
    {
        if (Database.ProviderName is null ||
            !Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return;

        Database.OpenConnection();
        var exists = false;
        using (var cmd = Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText =
                "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'VehicleInspections' LIMIT 1";
            exists = cmd.ExecuteScalar() is not null;
        }

        if (!exists)
        {
            Database.ExecuteSqlRaw("""
                CREATE TABLE "VehicleInspections" (
                    "InspectionId" INTEGER NOT NULL CONSTRAINT "PK_VehicleInspections" PRIMARY KEY AUTOINCREMENT,
                    "BookingId" INTEGER NOT NULL,
                    "VehicleId" INTEGER NOT NULL,
                    "InspectionType" TEXT NOT NULL,
                    "ActualAt" TEXT NOT NULL,
                    "OdometerKm" TEXT NULL,
                    "FuelLevel" TEXT NULL,
                    "Condition" TEXT NULL,
                    "Notes" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    CONSTRAINT "FK_VehicleInspections_Bookings_BookingId" FOREIGN KEY ("BookingId") REFERENCES "Bookings" ("BookingId") ON DELETE CASCADE,
                    CONSTRAINT "FK_VehicleInspections_Vehicles_VehicleId" FOREIGN KEY ("VehicleId") REFERENCES "Vehicles" ("VehicleId") ON DELETE RESTRICT
                );
                """);
        }

        Database.ExecuteSqlRaw(
            """CREATE INDEX IF NOT EXISTS "IX_VehicleInspections_BookingId" ON "VehicleInspections" ("BookingId");""");
        Database.ExecuteSqlRaw(
            """CREATE INDEX IF NOT EXISTS "IX_VehicleInspections_VehicleId" ON "VehicleInspections" ("VehicleId");""");
    }

    /// <summary>
    /// Adds FinalAmount to existing SQLite Bookings. Existing rows stay NULL (no backfill).
    /// </summary>
    public void EnsureSqliteBookingFinalAmountColumn()
    {
        if (Database.ProviderName is null ||
            !Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return;

        Database.OpenConnection();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('Bookings')";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                columns.Add(reader.GetString(1));
        }

        if (!columns.Contains("FinalAmount"))
            Database.ExecuteSqlRaw("ALTER TABLE Bookings ADD COLUMN FinalAmount TEXT NULL");
    }

    /// <summary>
    /// Creates BookingFees on an existing SQLite database. No backfill.
    /// </summary>
    public void EnsureSqliteBookingFeesTable()
    {
        if (Database.ProviderName is null ||
            !Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return;

        Database.OpenConnection();
        var exists = false;
        using (var cmd = Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText =
                "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'BookingFees' LIMIT 1";
            exists = cmd.ExecuteScalar() is not null;
        }

        if (!exists)
        {
            Database.ExecuteSqlRaw("""
                CREATE TABLE "BookingFees" (
                    "FeeId" INTEGER NOT NULL CONSTRAINT "PK_BookingFees" PRIMARY KEY AUTOINCREMENT,
                    "BookingId" INTEGER NOT NULL,
                    "FeeType" TEXT NOT NULL,
                    "Description" TEXT NULL,
                    "Amount" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    CONSTRAINT "FK_BookingFees_Bookings_BookingId" FOREIGN KEY ("BookingId") REFERENCES "Bookings" ("BookingId") ON DELETE CASCADE
                );
                """);
        }

        Database.ExecuteSqlRaw(
            """CREATE INDEX IF NOT EXISTS "IX_BookingFees_BookingId" ON "BookingFees" ("BookingId");""");
    }

    public void EnsureSqliteUsersLockColumn()
    {
        if (Database.ProviderName is null ||
            !Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return;

        Database.OpenConnection();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('Users')";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                columns.Add(reader.GetString(1));
        }

        if (!columns.Contains("IsLocked"))
            Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN IsLocked INTEGER NOT NULL DEFAULT 0");
    }

    /// <summary>
    /// Adds current lock/inactive reason columns on existing SQLite Users. No history table.
    /// </summary>
    public void EnsureSqliteUsersAccountStatusColumns()
    {
        if (Database.ProviderName is null ||
            !Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return;

        Database.OpenConnection();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('Users')";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                columns.Add(reader.GetString(1));
        }

        if (columns.Count == 0)
            return;

        if (!columns.Contains("LockReason"))
            Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN LockReason TEXT NULL");
        if (!columns.Contains("LockedAt"))
            Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN LockedAt TEXT NULL");
        if (!columns.Contains("LockedByUserId"))
            Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN LockedByUserId INTEGER NULL");
        if (!columns.Contains("InactiveReason"))
            Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN InactiveReason TEXT NULL");
        if (!columns.Contains("InactivatedAt"))
            Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN InactivatedAt TEXT NULL");
        if (!columns.Contains("InactivatedByUserId"))
            Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN InactivatedByUserId INTEGER NULL");
    }

    /// <summary>
    /// Adds email-verification / Google identity columns on existing SQLite Users.
    /// Default IsEmailVerified=1 so seed/demo accounts stay able to log in.
    /// </summary>
    public void EnsureSqliteUsersAuthColumns()
    {
        if (Database.ProviderName is null ||
            !Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return;

        Database.OpenConnection();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('Users')";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                columns.Add(reader.GetString(1));
        }

        if (columns.Count == 0)
            return;

        if (!columns.Contains("IsEmailVerified"))
            Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN IsEmailVerified INTEGER NOT NULL DEFAULT 1");
        if (!columns.Contains("EmailVerifiedAt"))
            Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN EmailVerifiedAt TEXT NULL");
        if (!columns.Contains("GoogleSubject"))
            Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN GoogleSubject TEXT NULL");

        Database.ExecuteSqlRaw(
            """CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_GoogleSubject" ON "Users" ("GoogleSubject") WHERE "GoogleSubject" IS NOT NULL;""");
    }

    /// <summary>
    /// Creates EmailOtps on an existing SQLite database. No backfill.
    /// </summary>
    public void EnsureSqliteEmailOtpsTable()
    {
        if (Database.ProviderName is null ||
            !Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return;

        Database.OpenConnection();
        var exists = false;
        using (var cmd = Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText =
                "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'EmailOtps' LIMIT 1";
            exists = cmd.ExecuteScalar() is not null;
        }

        if (!exists)
        {
            Database.ExecuteSqlRaw("""
                CREATE TABLE "EmailOtps" (
                    "EmailOtpId" INTEGER NOT NULL CONSTRAINT "PK_EmailOtps" PRIMARY KEY AUTOINCREMENT,
                    "Email" TEXT NOT NULL,
                    "Purpose" TEXT NOT NULL,
                    "CodeHash" TEXT NOT NULL,
                    "ExpiresAt" TEXT NOT NULL,
                    "AttemptCount" INTEGER NOT NULL,
                    "UsedAt" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL
                );
                """);
        }

        Database.ExecuteSqlRaw(
            """CREATE INDEX IF NOT EXISTS "IX_EmailOtps_Email_Purpose_CreatedAt" ON "EmailOtps" ("Email", "Purpose", "CreatedAt");""");
    }

    public void EnsureSqliteDriversActiveColumn()
    {
        if (Database.ProviderName is null ||
            !Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return;

        Database.OpenConnection();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('Drivers')";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                columns.Add(reader.GetString(1));
        }

        if (!columns.Contains("IsActive"))
            Database.ExecuteSqlRaw("ALTER TABLE Drivers ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 1");
    }

    public void EnsureSqliteBookingRequestedDriverColumn()
    {
        if (Database.ProviderName is null ||
            !Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return;

        Database.OpenConnection();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('Bookings')";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                columns.Add(reader.GetString(1));
        }

        if (!columns.Contains("RequestedDriverId"))
            Database.ExecuteSqlRaw("ALTER TABLE Bookings ADD COLUMN RequestedDriverId INTEGER NULL");
    }

    public void EnsureSqliteMaintenanceRecordsTable()
    {
        if (Database.ProviderName is null ||
            !Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return;

        Database.OpenConnection();
        var exists = false;
        using (var cmd = Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText =
                "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'MaintenanceRecords' LIMIT 1";
            exists = cmd.ExecuteScalar() is not null;
        }

        if (!exists)
        {
            Database.ExecuteSqlRaw("""
                CREATE TABLE "MaintenanceRecords" (
                    "MaintenanceId" INTEGER NOT NULL CONSTRAINT "PK_MaintenanceRecords" PRIMARY KEY AUTOINCREMENT,
                    "VehicleId" INTEGER NOT NULL,
                    "MaintenanceType" TEXT NOT NULL,
                    "ScheduledDate" TEXT NOT NULL,
                    "CompletedDate" TEXT NULL,
                    "OdometerAtMaintenance" INTEGER NULL,
                    "Cost" TEXT NULL,
                    "Notes" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    CONSTRAINT "FK_MaintenanceRecords_Vehicles_VehicleId" FOREIGN KEY ("VehicleId") REFERENCES "Vehicles" ("VehicleId") ON DELETE RESTRICT
                );
                """);
        }

        Database.ExecuteSqlRaw(
            """CREATE INDEX IF NOT EXISTS "IX_MaintenanceRecords_VehicleId" ON "MaintenanceRecords" ("VehicleId");""");
    }

    public void EnsureSqliteBookingRecommendationColumn()
    {
        if (Database.ProviderName is null ||
            !Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return;

        Database.OpenConnection();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('Bookings')";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                columns.Add(reader.GetString(1));
        }

        if (!columns.Contains("SourceRecommended"))
            Database.ExecuteSqlRaw("ALTER TABLE Bookings ADD COLUMN SourceRecommended INTEGER NOT NULL DEFAULT 0");
    }

    /// <summary>
    /// Creates Contracts on an existing SQLite database. EnsureCreated does not add new tables to a live DB.
    /// No backfill and no seed overwrite.
    /// </summary>
    public void EnsureSqliteContractsTable()
    {
        if (Database.ProviderName is null ||
            !Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return;

        Database.OpenConnection();
        var exists = false;
        using (var cmd = Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText =
                "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'Contracts' LIMIT 1";
            exists = cmd.ExecuteScalar() is not null;
        }

        if (!exists)
        {
            Database.ExecuteSqlRaw("""
                CREATE TABLE "Contracts" (
                    "ContractId" INTEGER NOT NULL CONSTRAINT "PK_Contracts" PRIMARY KEY AUTOINCREMENT,
                    "BookingId" INTEGER NOT NULL,
                    "ContractNumber" TEXT NOT NULL,
                    "Status" TEXT NOT NULL,
                    "CustomerId" INTEGER NOT NULL,
                    "CustomerName" TEXT NOT NULL,
                    "VehicleTypeName" TEXT NOT NULL,
                    "RentalMode" TEXT NOT NULL,
                    "PickupAddress" TEXT NOT NULL,
                    "DropoffAddress" TEXT NOT NULL,
                    "StartDate" TEXT NOT NULL,
                    "EndDate" TEXT NOT NULL,
                    "TotalAmount" TEXT NOT NULL,
                    "DepositAmount" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "SignedAt" TEXT NULL,
                    CONSTRAINT "FK_Contracts_Bookings_BookingId" FOREIGN KEY ("BookingId") REFERENCES "Bookings" ("BookingId") ON DELETE RESTRICT
                );
                """);
        }

        Database.ExecuteSqlRaw(
            """CREATE UNIQUE INDEX IF NOT EXISTS "IX_Contracts_BookingId" ON "Contracts" ("BookingId");""");
        Database.ExecuteSqlRaw(
            """CREATE UNIQUE INDEX IF NOT EXISTS "IX_Contracts_ContractNumber" ON "Contracts" ("ContractNumber");""");
    }

    /// <summary>
    /// Adds ExteriorCondition / TechnicalCondition on existing SQLite VehicleInspections.
    /// </summary>
    public void EnsureSqliteInspectionConditionColumns()
    {
        if (Database.ProviderName is null ||
            !Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return;

        Database.OpenConnection();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('VehicleInspections')";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                columns.Add(reader.GetString(1));
        }

        if (columns.Count == 0)
            return;

        if (!columns.Contains("ExteriorCondition"))
            Database.ExecuteSqlRaw("ALTER TABLE VehicleInspections ADD COLUMN ExteriorCondition TEXT NULL");
        if (!columns.Contains("TechnicalCondition"))
            Database.ExecuteSqlRaw("ALTER TABLE VehicleInspections ADD COLUMN TechnicalCondition TEXT NULL");
    }

    /// <summary>
    /// Creates IncidentReports on an existing SQLite database. No backfill.
    /// </summary>
    public void EnsureSqliteIncidentReportsTable()
    {
        if (Database.ProviderName is null ||
            !Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return;

        Database.OpenConnection();
        var exists = false;
        using (var cmd = Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText =
                "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'IncidentReports' LIMIT 1";
            exists = cmd.ExecuteScalar() is not null;
        }

        if (!exists)
        {
            Database.ExecuteSqlRaw("""
                CREATE TABLE "IncidentReports" (
                    "IncidentId" INTEGER NOT NULL CONSTRAINT "PK_IncidentReports" PRIMARY KEY AUTOINCREMENT,
                    "BookingId" INTEGER NOT NULL,
                    "AssignmentId" INTEGER NOT NULL,
                    "DriverId" INTEGER NOT NULL,
                    "IncidentType" TEXT NOT NULL,
                    "Description" TEXT NOT NULL,
                    "OccurredAt" TEXT NOT NULL,
                    "Status" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    CONSTRAINT "FK_IncidentReports_Bookings_BookingId" FOREIGN KEY ("BookingId") REFERENCES "Bookings" ("BookingId") ON DELETE RESTRICT,
                    CONSTRAINT "FK_IncidentReports_TripAssignments_AssignmentId" FOREIGN KEY ("AssignmentId") REFERENCES "TripAssignments" ("AssignmentId") ON DELETE RESTRICT,
                    CONSTRAINT "FK_IncidentReports_Drivers_DriverId" FOREIGN KEY ("DriverId") REFERENCES "Drivers" ("DriverId") ON DELETE RESTRICT
                );
                """);
        }

        Database.ExecuteSqlRaw(
            """CREATE INDEX IF NOT EXISTS "IX_IncidentReports_BookingId" ON "IncidentReports" ("BookingId");""");
        Database.ExecuteSqlRaw(
            """CREATE INDEX IF NOT EXISTS "IX_IncidentReports_DriverId" ON "IncidentReports" ("DriverId");""");
        Database.ExecuteSqlRaw(
            """CREATE INDEX IF NOT EXISTS "IX_IncidentReports_AssignmentId" ON "IncidentReports" ("AssignmentId");""");
    }

    /// <summary>
    /// Adds legal circulation metadata on existing SQLite Vehicles. Existing rows stay NULL.
    /// </summary>
    public void EnsureSqliteVehicleLegalColumns()
    {
        if (Database.ProviderName is null ||
            !Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return;

        Database.OpenConnection();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('Vehicles')";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                columns.Add(reader.GetString(1));
        }

        if (columns.Count == 0)
            return;

        if (!columns.Contains("RegistrationNumber"))
            Database.ExecuteSqlRaw("ALTER TABLE Vehicles ADD COLUMN RegistrationNumber TEXT NULL");
        if (!columns.Contains("RegistrationExpiryDate"))
            Database.ExecuteSqlRaw("ALTER TABLE Vehicles ADD COLUMN RegistrationExpiryDate TEXT NULL");
        if (!columns.Contains("InspectionExpiryDate"))
            Database.ExecuteSqlRaw("ALTER TABLE Vehicles ADD COLUMN InspectionExpiryDate TEXT NULL");
        if (!columns.Contains("InsuranceExpiryDate"))
            Database.ExecuteSqlRaw("ALTER TABLE Vehicles ADD COLUMN InsuranceExpiryDate TEXT NULL");
    }

    /// <summary>
    /// Case-insensitive unique index on LicensePlate. Skips when duplicate groups exist
    /// so an existing database is never reset or repaired.
    /// </summary>
    public void EnsureSqliteLicensePlateUniqueIndex()
    {
        if (Database.ProviderName is null ||
            !Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return;

        Database.OpenConnection();
        var tableExists = false;
        using (var cmd = Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText =
                "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'Vehicles' LIMIT 1";
            tableExists = cmd.ExecuteScalar() is not null;
        }

        if (!tableExists)
            return;

        var hasDuplicates = false;
        using (var cmd = Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = """
                SELECT 1
                FROM Vehicles
                GROUP BY LOWER(TRIM(LicensePlate))
                HAVING COUNT(*) > 1
                LIMIT 1
                """;
            hasDuplicates = cmd.ExecuteScalar() is not null;
        }

        if (hasDuplicates)
            return;

        Database.ExecuteSqlRaw(
            """CREATE UNIQUE INDEX IF NOT EXISTS "IX_Vehicles_LicensePlate" ON "Vehicles" ("LicensePlate" COLLATE NOCASE);""");
    }

    /// <summary>
    /// Fills NULL VehicleType pricing columns. Does not touch Bookings or Payments.
    /// </summary>
    public void FillVehicleTypePricingDefaults()
    {
        var types = VehicleTypes.ToList();
        foreach (var vt in types)
        {
            vt.DriverFeePerDay ??= PricingDefaults.DriverFeePerDay(vt.PricePerDay);
            vt.SelfDrivePricePerDay ??= PricingDefaults.SelfDrivePricePerDay(vt.PricePerDay);
            vt.SelfDriveIncludedKmPerDay ??= PricingDefaults.IncludedKmPerDay;
            vt.SelfDriveExtraKmPrice ??= PricingDefaults.ExtraKmPrice(vt.PricePerKm);
            vt.WithDriverDepositAmount ??= PricingDefaults.WithDriverDeposit(vt.PricePerDay);
            vt.SelfDriveDepositAmount ??= PricingDefaults.SelfDriveDeposit(vt.PricePerDay);
        }

        SaveChanges();
    }

    /// <summary>
    /// Aligns Driver/Vehicle status with open TripAssignments without touching bookings or assignments.
    /// Does not change Offline drivers or non-Available vehicles (e.g. Maintenance).
    /// </summary>
    public void ReconcileOpenAssignmentResourceStatus()
    {
        var open = TripAssignments
            .Where(t =>
                t.Status == TripAssignmentStatuses.Assigned
                || t.Status == TripAssignmentStatuses.Accepted
                || t.Status == TripAssignmentStatuses.InProgress)
            .Select(t => new { t.DriverId, t.VehicleId })
            .ToList();

        foreach (var row in open)
        {
            var driver = Drivers.Find(row.DriverId);
            if (driver is not null && driver.Status == DriverStatuses.Available)
                driver.Status = DriverStatuses.Busy;

            var vehicle = Vehicles.Find(row.VehicleId);
            if (vehicle is not null && vehicle.Status == VehicleStatuses.Available)
                vehicle.Status = VehicleStatuses.Rented;
        }

        SaveChanges();
    }

    /// <summary>
    /// Phase 1 final schema for an existing SQLite file.
    /// Adds missing columns and the five tables that are not on the EF model.
    /// Does not drop, rebuild, or rewrite existing rows. RequestedDriverId is left to its existing helper.
    /// </summary>
    public void EnsureSqlitePhase1FinalSchema()
    {
        if (Database.ProviderName is null ||
            !Database.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return;

        Database.OpenConnection();

        AddMissingColumns("Customers",
        [
            ("DriverLicenseNumber", "TEXT NULL"),
            ("DriverLicenseClass", "TEXT NULL"),
            ("DriverLicenseExpiry", "TEXT NULL"),
            ("VerificationStatus", "TEXT NOT NULL DEFAULT 'Pending'"),
            ("MembershipTier", "TEXT NOT NULL DEFAULT 'Standard'"),
            ("JoinedAt", "TEXT NULL"),
            ("Occupation", "TEXT NULL"),
            ("IncomeRange", "TEXT NULL"),
            ("FamilySize", "INTEGER NULL"),
            ("NumberOfChildren", "INTEGER NULL"),
            ("CityType", "TEXT NULL"),
            ("AnnualMileage", "INTEGER NULL"),
            ("CommuteKmPerDay", "REAL NULL"),
            ("RentalPurpose", "TEXT NULL"),
            ("PreferredVehicleType", "TEXT NULL"),
            ("PreferredFuelType", "TEXT NULL"),
            ("PreferredTransmission", "TEXT NULL"),
            ("PreferredSeats", "INTEGER NULL"),
            ("BudgetMin", "REAL NULL"),
            ("BudgetMax", "REAL NULL"),
            ("PreferencesText", "TEXT NULL"),
            ("AvatarUrl", "TEXT NULL")
        ]);

        AddMissingColumns("Drivers",
        [
            ("LicenseClass", "TEXT NULL"),
            ("LicenseIssueDate", "TEXT NULL"),
            ("IdNumber", "TEXT NULL"),
            ("DateOfBirth", "TEXT NULL"),
            ("Address", "TEXT NULL"),
            ("AvatarUrl", "TEXT NULL"),
            ("ExperienceYears", "REAL NULL"),
            ("OnTimeRate", "REAL NULL"),
            ("TotalDistanceKm", "REAL NULL"),
            ("VerificationStatus", "TEXT NOT NULL DEFAULT 'Pending'")
        ]);

        AddMissingColumns("Vehicles",
        [
            ("Transmission", "TEXT NULL"),
            ("FuelType", "TEXT NULL"),
            ("TrunkCapacity", "REAL NULL"),
            ("Engine", "TEXT NULL"),
            ("Horsepower", "INTEGER NULL"),
            ("FuelConsumption", "REAL NULL"),
            ("SafetyScore", "REAL NULL"),
            ("ConditionStatus", "TEXT NOT NULL DEFAULT 'Good'"),
            ("FuelLevel", "REAL NULL"),
            ("Notes", "TEXT NULL"),
            ("LastMaintenanceDate", "TEXT NULL"),
            ("LastMaintenanceOdometer", "INTEGER NULL"),
            ("MaintenanceIntervalKm", "INTEGER NULL"),
            ("MaintenanceIntervalDays", "INTEGER NULL"),
            ("NextMaintenanceDate", "TEXT NULL"),
            ("NextMaintenanceOdometer", "INTEGER NULL")
        ]);

        AddMissingColumns("Reviews",
        [
            ("ComfortRating", "INTEGER NULL"),
            ("CleanlinessRating", "INTEGER NULL"),
            ("PerformanceRating", "INTEGER NULL"),
            ("ValueRating", "INTEGER NULL")
        ]);

        AddMissingColumns("MaintenanceRecords",
        [
            ("ScheduledOdometer", "INTEGER NULL"),
            ("NextMaintenanceDate", "TEXT NULL"),
            ("NextMaintenanceOdometer", "INTEGER NULL")
        ]);

        AddMissingColumns("EmailOtps",
        [
            ("UserId", "INTEGER NULL")
        ]);

        EnsureTable("Notifications", """
            CREATE TABLE "Notifications" (
                "NotificationId" INTEGER NOT NULL CONSTRAINT "PK_Notifications" PRIMARY KEY AUTOINCREMENT,
                "UserId" INTEGER NOT NULL,
                "Title" TEXT NOT NULL,
                "Message" TEXT NOT NULL,
                "Type" TEXT NULL,
                "IsRead" INTEGER NOT NULL DEFAULT 0,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_Notifications_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("UserId") ON DELETE RESTRICT
            );
            """);

        EnsureTable("VehicleStatusHistory", """
            CREATE TABLE "VehicleStatusHistory" (
                "StatusHistoryId" INTEGER NOT NULL CONSTRAINT "PK_VehicleStatusHistory" PRIMARY KEY AUTOINCREMENT,
                "VehicleId" INTEGER NOT NULL,
                "OldStatus" TEXT NULL,
                "NewStatus" TEXT NOT NULL,
                "Reason" TEXT NULL,
                "ChangedByUserId" INTEGER NULL,
                "ChangedAt" TEXT NOT NULL,
                CONSTRAINT "FK_VehicleStatusHistory_Vehicles_VehicleId" FOREIGN KEY ("VehicleId") REFERENCES "Vehicles" ("VehicleId") ON DELETE RESTRICT,
                CONSTRAINT "FK_VehicleStatusHistory_Users_ChangedByUserId" FOREIGN KEY ("ChangedByUserId") REFERENCES "Users" ("UserId") ON DELETE SET NULL
            );
            """);

        EnsureTable("RecommendationLogs", """
            CREATE TABLE "RecommendationLogs" (
                "RecommendationId" INTEGER NOT NULL CONSTRAINT "PK_RecommendationLogs" PRIMARY KEY AUTOINCREMENT,
                "CustomerId" INTEGER NOT NULL,
                "VehicleId" INTEGER NOT NULL,
                "Score" REAL NOT NULL,
                "RecommendationReason" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_RecommendationLogs_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("CustomerId") ON DELETE RESTRICT,
                CONSTRAINT "FK_RecommendationLogs_Vehicles_VehicleId" FOREIGN KEY ("VehicleId") REFERENCES "Vehicles" ("VehicleId") ON DELETE RESTRICT
            );
            """);

        EnsureTable("VehicleImages", """
            CREATE TABLE "VehicleImages" (
                "VehicleImageId" INTEGER NOT NULL CONSTRAINT "PK_VehicleImages" PRIMARY KEY AUTOINCREMENT,
                "VehicleId" INTEGER NOT NULL,
                "ImageUrl" TEXT NOT NULL,
                "ImageType" TEXT NULL,
                "IsPrimary" INTEGER NOT NULL DEFAULT 0,
                "DisplayOrder" INTEGER NOT NULL DEFAULT 0,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_VehicleImages_Vehicles_VehicleId" FOREIGN KEY ("VehicleId") REFERENCES "Vehicles" ("VehicleId") ON DELETE RESTRICT
            );
            """);

        EnsureTable("CustomerVehicleInteractions", """
            CREATE TABLE "CustomerVehicleInteractions" (
                "InteractionId" INTEGER NOT NULL CONSTRAINT "PK_CustomerVehicleInteractions" PRIMARY KEY AUTOINCREMENT,
                "CustomerId" INTEGER NOT NULL,
                "VehicleId" INTEGER NOT NULL,
                "InteractionType" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_CustomerVehicleInteractions_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("CustomerId") ON DELETE RESTRICT,
                CONSTRAINT "FK_CustomerVehicleInteractions_Vehicles_VehicleId" FOREIGN KEY ("VehicleId") REFERENCES "Vehicles" ("VehicleId") ON DELETE RESTRICT
            );
            """);

        EnsureIndex("IX_Customers_VerificationStatus", "Customers", "\"VerificationStatus\"");
        EnsureIndex("IX_Customers_MembershipTier", "Customers", "\"MembershipTier\"");
        EnsureIndex("IX_Drivers_Status", "Drivers", "\"Status\"");
        EnsureIndex("IX_Drivers_IsActive", "Drivers", "\"IsActive\"");
        EnsureIndex("IX_Vehicles_TypeId", "Vehicles", "\"TypeId\"");
        EnsureIndex("IX_Vehicles_Status", "Vehicles", "\"Status\"");
        EnsureIndex("IX_Vehicles_FuelType", "Vehicles", "\"FuelType\"");
        EnsureIndex("IX_Vehicles_Transmission", "Vehicles", "\"Transmission\"");
        EnsureIndex("IX_Bookings_CustomerId", "Bookings", "\"CustomerId\"");
        EnsureIndex("IX_Bookings_VehicleTypeId", "Bookings", "\"VehicleTypeId\"");
        EnsureIndex("IX_Bookings_AssignedVehicleId", "Bookings", "\"AssignedVehicleId\"");
        EnsureIndex("IX_Bookings_RequestedDriverId", "Bookings", "\"RequestedDriverId\"");
        EnsureIndex("IX_Bookings_Status", "Bookings", "\"Status\"");
        EnsureIndex("IX_Bookings_StartDate_EndDate", "Bookings", "\"StartDate\", \"EndDate\"");
        EnsureIndex("IX_TripAssignments_DriverId", "TripAssignments", "\"DriverId\"");
        EnsureIndex("IX_TripAssignments_VehicleId", "TripAssignments", "\"VehicleId\"");
        EnsureIndex("IX_VehicleImages_VehicleId", "VehicleImages", "\"VehicleId\"");
        EnsureIndex("IX_CustomerVehicleInteractions_CustomerId", "CustomerVehicleInteractions", "\"CustomerId\"");
        EnsureIndex("IX_CustomerVehicleInteractions_VehicleId", "CustomerVehicleInteractions", "\"VehicleId\"");
        EnsureIndex("IX_CustomerVehicleInteractions_Type_CreatedAt", "CustomerVehicleInteractions", "\"InteractionType\", \"CreatedAt\"");
        EnsureIndex("IX_RecommendationLogs_CustomerId", "RecommendationLogs", "\"CustomerId\"");
        EnsureIndex("IX_RecommendationLogs_VehicleId", "RecommendationLogs", "\"VehicleId\"");
    }

    private void AddMissingColumns(string table, (string Name, string Definition)[] columns)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = $"PRAGMA table_info('{table}')";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                existing.Add(reader.GetString(1));
        }

        if (existing.Count == 0)
            return;

        foreach (var (name, definition) in columns)
        {
            if (existing.Contains(name))
                continue;
            var addColumn = "ALTER TABLE \"" + table + "\" ADD COLUMN \"" + name + "\" " + definition;
            Database.ExecuteSqlRaw(addColumn);
        }
    }

    private void EnsureTable(string table, string createSql)
    {
        using var cmd = Database.GetDbConnection().CreateCommand();
        cmd.CommandText =
            "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1";
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = table;
        cmd.Parameters.Add(parameter);
        if (cmd.ExecuteScalar() is not null)
            return;

        Database.ExecuteSqlRaw(createSql);
    }

    private void EnsureIndex(string indexName, string table, string columns)
    {
        using var cmd = Database.GetDbConnection().CreateCommand();
        cmd.CommandText =
            "SELECT 1 FROM sqlite_master WHERE type = 'index' AND name = $name LIMIT 1";
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = indexName;
        cmd.Parameters.Add(parameter);
        if (cmd.ExecuteScalar() is not null)
            return;

        var createIndex = "CREATE INDEX \"" + indexName + "\" ON \"" + table + "\" (" + columns + ")";
        Database.ExecuteSqlRaw(createIndex);
    }
}
