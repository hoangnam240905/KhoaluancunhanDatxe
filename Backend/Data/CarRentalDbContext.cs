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
}
