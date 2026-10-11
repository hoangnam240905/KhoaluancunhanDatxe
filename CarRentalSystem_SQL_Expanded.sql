-- CarRentalSystem - SQL Server version converted from SQLite schema
-- Source: sql.txt (SQLite)
-- 20 tables, foreign keys and indexes preserved.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF DB_ID(N'CarRentalSystem') IS NULL
BEGIN
    CREATE DATABASE [CarRentalSystem];
END;
GO

USE [CarRentalSystem];
GO

-- ============================================================================
-- Roles
-- ============================================================================
IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[Roles] (
    
        [RoleId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Roles] PRIMARY KEY,
    
        [RoleName] NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(1000) NULL
    );
END;
GO

-- ============================================================================
-- Users
-- ============================================================================
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[Users] (
    
        [UserId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Users] PRIMARY KEY,
    
        [Email] NVARCHAR(320) NOT NULL,
        [PasswordHash] NVARCHAR(255) NOT NULL,
        [FullName] NVARCHAR(255) NOT NULL,
        [Phone] NVARCHAR(30) NULL,
    
        [RoleId] INT NOT NULL,
    
        [IsActive] INT NOT NULL DEFAULT 1,
        [IsLocked] INT NOT NULL DEFAULT 0,
    
        [LockReason] NVARCHAR(1000) NULL,
        [LockedAt] NVARCHAR(50) NULL,
        [LockedByUserId] INT NULL,
    
        [InactiveReason] NVARCHAR(1000) NULL,
        [InactivatedAt] NVARCHAR(50) NULL,
        [InactivatedByUserId] INT NULL,
    
        [IsEmailVerified] INT NOT NULL DEFAULT 1,
        [EmailVerifiedAt] NVARCHAR(50) NULL,
    
        [GoogleSubject] NVARCHAR(255) NULL,
    
        [CreatedAt] NVARCHAR(50) NOT NULL,
        [UpdatedAt] NVARCHAR(50) NULL,
    
        CONSTRAINT [FK_Users_Roles_RoleId]
            FOREIGN KEY ([RoleId])
            REFERENCES [Roles] ([RoleId])
    );
END;
GO

-- ============================================================================
-- EmailOtps
-- ============================================================================
IF OBJECT_ID(N'dbo.EmailOtps', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[EmailOtps] (
    
        [EmailOtpId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_EmailOtps] PRIMARY KEY,

        [UserId] INT NULL,
    
        [Email] NVARCHAR(320) NOT NULL,
        [Purpose] NVARCHAR(50) NOT NULL,
    
        [CodeHash] NVARCHAR(255) NOT NULL,
    
        [ExpiresAt] NVARCHAR(50) NOT NULL,
    
        [AttemptCount] INT NOT NULL DEFAULT 0,
    
        [UsedAt] NVARCHAR(50) NULL,
    
        [CreatedAt] NVARCHAR(50) NOT NULL,

        CONSTRAINT [FK_EmailOtps_Users_UserId]
            FOREIGN KEY ([UserId])
            REFERENCES [Users] ([UserId])
    );
END;
GO

-- ============================================================================

-- ============================================================================
-- EmailOtps relationship migration
-- Users (1) ---- (N) EmailOtps
-- UserId is nullable so OTP records can still exist before account creation.
-- ============================================================================
IF COL_LENGTH(N'dbo.EmailOtps', N'UserId') IS NULL
BEGIN
    ALTER TABLE dbo.[EmailOtps]
        ADD [UserId] INT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_EmailOtps_Users_UserId'
      AND parent_object_id = OBJECT_ID(N'dbo.EmailOtps')
)
BEGIN
    ALTER TABLE dbo.[EmailOtps]
        ADD CONSTRAINT [FK_EmailOtps_Users_UserId]
        FOREIGN KEY ([UserId])
        REFERENCES dbo.[Users] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_EmailOtps_UserId'
      AND object_id = OBJECT_ID(N'dbo.EmailOtps')
)
BEGIN
    CREATE INDEX [IX_EmailOtps_UserId]
        ON dbo.[EmailOtps] ([UserId]);
END;
GO

-- Customers
-- ============================================================================
IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[Customers] (
    
        [CustomerId] INT NOT NULL
            CONSTRAINT [PK_Customers]
            PRIMARY KEY,
    
        [Address] NVARCHAR(1000) NULL,
        [IdNumber] NVARCHAR(50) NULL,
        [DateOfBirth] NVARCHAR(20) NULL,
    
        CONSTRAINT [FK_Customers_Users_CustomerId]
            FOREIGN KEY ([CustomerId])
            REFERENCES [Users] ([UserId])
    );
END;
GO

-- ============================================================================
-- Drivers
-- ============================================================================
IF OBJECT_ID(N'dbo.Drivers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[Drivers] (
    
        [DriverId] INT NOT NULL
            CONSTRAINT [PK_Drivers]
            PRIMARY KEY,
    
        [LicenseNumber] NVARCHAR(50) NOT NULL,
        [LicenseExpiry] NVARCHAR(20) NOT NULL,
    
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Offline',
    
        [AverageRating] NVARCHAR(50) NOT NULL DEFAULT '0',
        [TotalTrips] INT NOT NULL DEFAULT 0,
    
        [IsActive] INT NOT NULL DEFAULT 1,
    
        CONSTRAINT [FK_Drivers_Users_DriverId]
            FOREIGN KEY ([DriverId])
            REFERENCES [Users] ([UserId])
    );
END;
GO

-- ============================================================================
-- VehicleTypes
-- ============================================================================
IF OBJECT_ID(N'dbo.VehicleTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[VehicleTypes] (
    
        [TypeId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_VehicleTypes] PRIMARY KEY,
    
        [TypeName] NVARCHAR(100) NOT NULL,
    
        [SeatCapacity] INT NOT NULL,
    
        [PricePerDay] NVARCHAR(50) NOT NULL,
        [PricePerKm] NVARCHAR(50) NOT NULL,
    
        [DriverFeePerDay] NVARCHAR(50) NULL,
    
        [SelfDrivePricePerDay] NVARCHAR(50) NULL,
    
        [SelfDriveIncludedKmPerDay] NVARCHAR(50) NULL,
    
        [SelfDriveExtraKmPrice] NVARCHAR(50) NULL,
    
        [WithDriverDepositAmount] NVARCHAR(50) NULL,
    
        [SelfDriveDepositAmount] NVARCHAR(50) NULL,
    
        [Description] NVARCHAR(1000) NULL,
    
        [ImageUrl] NVARCHAR(1000) NULL,
    
        [IsActive] INT NOT NULL DEFAULT 1
    );
END;
GO

-- ============================================================================
-- Vehicles
-- ============================================================================
IF OBJECT_ID(N'dbo.Vehicles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[Vehicles] (
    
        [VehicleId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Vehicles] PRIMARY KEY,
    
        [TypeId] INT NOT NULL,
    
        [LicensePlate] NVARCHAR(50) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
    
        [Brand] NVARCHAR(100) NOT NULL,
        [Model] NVARCHAR(100) NOT NULL,
    
        [Year] INT NOT NULL,
    
        [Color] NVARCHAR(50) NULL,
    
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Available',
    
        [CurrentKm] INT NOT NULL DEFAULT 0,
    
        [RegistrationNumber] NVARCHAR(100) NULL,
    
        [RegistrationExpiryDate] NVARCHAR(50) NULL,
    
        [InspectionExpiryDate] NVARCHAR(50) NULL,
    
        [InsuranceExpiryDate] NVARCHAR(50) NULL,
    
        [CreatedAt] NVARCHAR(50) NOT NULL,
    
        CONSTRAINT [FK_Vehicles_VehicleTypes_TypeId]
            FOREIGN KEY ([TypeId])
            REFERENCES [VehicleTypes] ([TypeId])
    );
END;
GO

-- ============================================================================
-- Bookings
-- ============================================================================
IF OBJECT_ID(N'dbo.Bookings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[Bookings] (
    
        [BookingId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Bookings] PRIMARY KEY,
    
        [CustomerId] INT NOT NULL,
    
        [VehicleTypeId] INT NOT NULL,
    
        [PickupAddress] NVARCHAR(1000) NOT NULL,
        [DropoffAddress] NVARCHAR(1000) NOT NULL,
    
        [PickupLat] NVARCHAR(50) NULL,
        [PickupLng] NVARCHAR(50) NULL,
    
        [DropoffLat] NVARCHAR(50) NULL,
        [DropoffLng] NVARCHAR(50) NULL,
    
        [StartDate] NVARCHAR(50) NOT NULL,
        [EndDate] NVARCHAR(50) NOT NULL,
    
        [EstimatedDistance] NVARCHAR(50) NULL,
    
        [TotalAmount] NVARCHAR(50) NOT NULL,
    
        [QuotedPricePerDay] NVARCHAR(50) NULL,
        [QuotedPricePerKm] NVARCHAR(50) NULL,
    
        [QuotedDays] INT NULL,
    
        [QuotedDriverFeePerDay] NVARCHAR(50) NULL,
    
        [QuotedSelfDriveIncludedKmPerDay] NVARCHAR(50) NULL,
    
        [QuotedSelfDriveExtraKmPrice] NVARCHAR(50) NULL,
    
        [QuotedDepositAmount] NVARCHAR(50) NULL,
    
        [FinalAmount] NVARCHAR(50) NULL,
    
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Pending',
    
        [RentalMode] NVARCHAR(50) NOT NULL DEFAULT 'WithDriver',
    
        [AssignedVehicleId] INT NULL,
    
        [SourceRecommended] INT NOT NULL DEFAULT 0,
    
        [Notes] NVARCHAR(1000) NULL,
    
        [CancellationReason] NVARCHAR(1000) NULL,
    
        [CreatedAt] NVARCHAR(50) NOT NULL,
        [UpdatedAt] NVARCHAR(50) NULL,
    
        CONSTRAINT [FK_Bookings_Customers_CustomerId]
            FOREIGN KEY ([CustomerId])
            REFERENCES [Customers] ([CustomerId]),
    
        CONSTRAINT [FK_Bookings_VehicleTypes_VehicleTypeId]
            FOREIGN KEY ([VehicleTypeId])
            REFERENCES [VehicleTypes] ([TypeId]),
    
        CONSTRAINT [FK_Bookings_Vehicles_AssignedVehicleId]
            FOREIGN KEY ([AssignedVehicleId])
            REFERENCES [Vehicles] ([VehicleId])
    );
END;
GO

-- ============================================================================
-- TripAssignments
-- ============================================================================
IF OBJECT_ID(N'dbo.TripAssignments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[TripAssignments] (
    
        [AssignmentId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_TripAssignments] PRIMARY KEY,
    
        [BookingId] INT NOT NULL,
    
        [DriverId] INT NOT NULL,
    
        [VehicleId] INT NOT NULL,
    
        [AssignedBy] INT NOT NULL,
    
        [AssignedAt] NVARCHAR(50) NOT NULL,
    
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Assigned',
    
        [StartedAt] NVARCHAR(50) NULL,
    
        [CompletedAt] NVARCHAR(50) NULL,
    
        CONSTRAINT [FK_TripAssignments_Bookings_BookingId]
            FOREIGN KEY ([BookingId])
            REFERENCES [Bookings] ([BookingId]),
    
        CONSTRAINT [FK_TripAssignments_Drivers_DriverId]
            FOREIGN KEY ([DriverId])
            REFERENCES [Drivers] ([DriverId]),
    
        CONSTRAINT [FK_TripAssignments_Vehicles_VehicleId]
            FOREIGN KEY ([VehicleId])
            REFERENCES [Vehicles] ([VehicleId]),
    
        CONSTRAINT [FK_TripAssignments_Users_AssignedBy]
            FOREIGN KEY ([AssignedBy])
            REFERENCES [Users] ([UserId])
    );
END;
GO

-- ============================================================================
-- BookingStatusHistory
-- ============================================================================
IF OBJECT_ID(N'dbo.BookingStatusHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[BookingStatusHistory] (
    
        [HistoryId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_BookingStatusHistory] PRIMARY KEY,
    
        [BookingId] INT NOT NULL,
    
        [OldStatus] NVARCHAR(50) NULL,
    
        [NewStatus] NVARCHAR(50) NOT NULL,
    
        [ChangedBy] INT NULL,
    
        [Note] NVARCHAR(1000) NULL,
    
        [ChangedAt] NVARCHAR(50) NOT NULL,
    
        CONSTRAINT [FK_BookingStatusHistory_Bookings_BookingId]
            FOREIGN KEY ([BookingId])
            REFERENCES [Bookings] ([BookingId]),
    
        CONSTRAINT [FK_BookingStatusHistory_Users_ChangedBy]
            FOREIGN KEY ([ChangedBy])
            REFERENCES [Users] ([UserId])
    );
END;
GO

-- ============================================================================
-- Payments
-- ============================================================================
IF OBJECT_ID(N'dbo.Payments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[Payments] (
    
        [PaymentId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Payments] PRIMARY KEY,
    
        [BookingId] INT NOT NULL,
    
        [PaymentType] NVARCHAR(50) NULL,
    
        [Amount] NVARCHAR(50) NOT NULL,
    
        [Method] NVARCHAR(50) NOT NULL,
    
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Pending',
    
        [TransactionRef] NVARCHAR(255) NULL,
    
        [PaidAt] NVARCHAR(50) NULL,
    
        [CreatedAt] NVARCHAR(50) NOT NULL,
    
        CONSTRAINT [FK_Payments_Bookings_BookingId]
            FOREIGN KEY ([BookingId])
            REFERENCES [Bookings] ([BookingId])
    );
END;
GO

-- ============================================================================
-- Contracts
-- ============================================================================
IF OBJECT_ID(N'dbo.Contracts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[Contracts] (
    
        [ContractId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Contracts] PRIMARY KEY,
    
        [BookingId] INT NOT NULL,
    
        [ContractNumber] NVARCHAR(100) NOT NULL,
    
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Issued',
    
        [CustomerId] INT NOT NULL,
    
        [CustomerName] NVARCHAR(255) NOT NULL,
    
        [VehicleTypeName] NVARCHAR(100) NOT NULL,
    
        [RentalMode] NVARCHAR(50) NOT NULL,
    
        [PickupAddress] NVARCHAR(1000) NOT NULL,
    
        [DropoffAddress] NVARCHAR(1000) NOT NULL,
    
        [StartDate] NVARCHAR(50) NOT NULL,
    
        [EndDate] NVARCHAR(50) NOT NULL,
    
        [TotalAmount] NVARCHAR(50) NOT NULL,
    
        [DepositAmount] NVARCHAR(50) NULL,
    
        [CreatedAt] NVARCHAR(50) NOT NULL,
    
        [SignedAt] NVARCHAR(50) NULL,
    
        CONSTRAINT [FK_Contracts_Bookings_BookingId]
            FOREIGN KEY ([BookingId])
            REFERENCES [Bookings] ([BookingId])
    );
END;
GO

-- ============================================================================
-- Reviews
-- ============================================================================
IF OBJECT_ID(N'dbo.Reviews', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[Reviews] (
    
        [ReviewId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Reviews] PRIMARY KEY,
    
        [BookingId] INT NOT NULL,
    
        [CustomerId] INT NOT NULL,
    
        [DriverId] INT NOT NULL,
    
        [Rating] INT NOT NULL,
    
        [Comment] NVARCHAR(2000) NULL,
    
        [CreatedAt] NVARCHAR(50) NOT NULL,
    
        CONSTRAINT [FK_Reviews_Bookings_BookingId]
            FOREIGN KEY ([BookingId])
            REFERENCES [Bookings] ([BookingId]),
    
        CONSTRAINT [FK_Reviews_Customers_CustomerId]
            FOREIGN KEY ([CustomerId])
            REFERENCES [Customers] ([CustomerId]),
    
        CONSTRAINT [FK_Reviews_Drivers_DriverId]
            FOREIGN KEY ([DriverId])
            REFERENCES [Drivers] ([DriverId])
    );
END;
GO

-- ============================================================================
-- VehicleInspections
-- ============================================================================
IF OBJECT_ID(N'dbo.VehicleInspections', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[VehicleInspections] (
    
        [InspectionId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_VehicleInspections] PRIMARY KEY,
    
        [BookingId] INT NOT NULL,
    
        [VehicleId] INT NOT NULL,
    
        [InspectionType] NVARCHAR(100) NOT NULL,
    
        [ActualAt] NVARCHAR(50) NOT NULL,
    
        [OdometerKm] NVARCHAR(50) NULL,
    
        [FuelLevel] NVARCHAR(50) NULL,
    
        [Condition] NVARCHAR(1000) NULL,
    
        [ExteriorCondition] NVARCHAR(1000) NULL,
    
        [TechnicalCondition] NVARCHAR(1000) NULL,
    
        [Notes] NVARCHAR(1000) NULL,
    
        [CreatedAt] NVARCHAR(50) NOT NULL,
    
        CONSTRAINT [FK_VehicleInspections_Bookings_BookingId]
            FOREIGN KEY ([BookingId])
            REFERENCES [Bookings] ([BookingId]),
    
        CONSTRAINT [FK_VehicleInspections_Vehicles_VehicleId]
            FOREIGN KEY ([VehicleId])
            REFERENCES [Vehicles] ([VehicleId])
    );
END;
GO

-- ============================================================================
-- BookingFees
-- ============================================================================
IF OBJECT_ID(N'dbo.BookingFees', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[BookingFees] (
    
        [FeeId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_BookingFees] PRIMARY KEY,
    
        [BookingId] INT NOT NULL,
    
        [FeeType] NVARCHAR(100) NOT NULL,
    
        [Description] NVARCHAR(1000) NULL,
    
        [Amount] NVARCHAR(50) NOT NULL,
    
        [CreatedAt] NVARCHAR(50) NOT NULL,
    
        CONSTRAINT [FK_BookingFees_Bookings_BookingId]
            FOREIGN KEY ([BookingId])
            REFERENCES [Bookings] ([BookingId])
    );
END;
GO

-- ============================================================================
-- MaintenanceRecords
-- ============================================================================
IF OBJECT_ID(N'dbo.MaintenanceRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[MaintenanceRecords] (
    
        [MaintenanceId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_MaintenanceRecords] PRIMARY KEY,
    
        [VehicleId] INT NOT NULL,
    
        [MaintenanceType] NVARCHAR(100) NOT NULL,
    
        [ScheduledDate] NVARCHAR(50) NOT NULL,
    
        [CompletedDate] NVARCHAR(50) NULL,
    
        [OdometerAtMaintenance] INT NULL,
    
        [Cost] NVARCHAR(50) NULL,
    
        [Notes] NVARCHAR(1000) NULL,
    
        [CreatedAt] NVARCHAR(50) NOT NULL,
    
        CONSTRAINT [FK_MaintenanceRecords_Vehicles_VehicleId]
            FOREIGN KEY ([VehicleId])
            REFERENCES [Vehicles] ([VehicleId])
    );
END;
GO

-- ============================================================================
-- IncidentReports
-- ============================================================================
IF OBJECT_ID(N'dbo.IncidentReports', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[IncidentReports] (
    
        [IncidentId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_IncidentReports] PRIMARY KEY,
    
        [BookingId] INT NOT NULL,
    
        [AssignmentId] INT NOT NULL,
    
        [DriverId] INT NOT NULL,
    
        [IncidentType] NVARCHAR(100) NOT NULL,
    
        [Description] NVARCHAR(1000) NOT NULL,
    
        [OccurredAt] NVARCHAR(50) NOT NULL,
    
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Open',
    
        [CreatedAt] NVARCHAR(50) NOT NULL,
    
        CONSTRAINT [FK_IncidentReports_Bookings_BookingId]
            FOREIGN KEY ([BookingId])
            REFERENCES [Bookings] ([BookingId]),
    
        CONSTRAINT [FK_IncidentReports_TripAssignments_AssignmentId]
            FOREIGN KEY ([AssignmentId])
            REFERENCES [TripAssignments] ([AssignmentId]),
    
        CONSTRAINT [FK_IncidentReports_Drivers_DriverId]
            FOREIGN KEY ([DriverId])
            REFERENCES [Drivers] ([DriverId])
    );
END;
GO

-- ============================================================================
-- Notifications
-- ============================================================================
IF OBJECT_ID(N'dbo.Notifications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[Notifications] (
    
        [NotificationId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Notifications] PRIMARY KEY,
    
        [UserId] INT NOT NULL,
    
        [Title] NVARCHAR(255) NOT NULL,
    
        [Message] NVARCHAR(2000) NOT NULL,
    
        [Type] NVARCHAR(100) NULL,
    
        [IsRead] INT NOT NULL DEFAULT 0,
    
        [CreatedAt] NVARCHAR(50) NOT NULL,
    
        CONSTRAINT [FK_Notifications_Users_UserId]
            FOREIGN KEY ([UserId])
            REFERENCES [Users] ([UserId])
            ON DELETE CASCADE
    );
END;
GO

-- ============================================================================
-- VehicleStatusHistory
-- ============================================================================
IF OBJECT_ID(N'dbo.VehicleStatusHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[VehicleStatusHistory] (
    
        [StatusHistoryId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_VehicleStatusHistory] PRIMARY KEY,
    
        [VehicleId] INT NOT NULL,
    
        [OldStatus] NVARCHAR(50) NULL,
    
        [NewStatus] NVARCHAR(50) NOT NULL,
    
        [Reason] NVARCHAR(1000) NULL,
    
        [ChangedByUserId] INT NULL,
    
        [ChangedAt] NVARCHAR(50) NOT NULL,
    
        CONSTRAINT [FK_VehicleStatusHistory_Vehicles_VehicleId]
            FOREIGN KEY ([VehicleId])
            REFERENCES [Vehicles] ([VehicleId]),
    
        CONSTRAINT [FK_VehicleStatusHistory_Users_ChangedByUserId]
            FOREIGN KEY ([ChangedByUserId])
            REFERENCES [Users] ([UserId])
            ON DELETE SET NULL
    );
END;
GO

-- ============================================================================
-- RecommendationLogs
-- ============================================================================
IF OBJECT_ID(N'dbo.RecommendationLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[RecommendationLogs] (
    
        [RecommendationId] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_RecommendationLogs] PRIMARY KEY,
    
        [CustomerId] INT NOT NULL,
    
        [VehicleId] INT NOT NULL,
    
        [Score] NVARCHAR(50) NOT NULL,
    
        [RecommendationReason] NVARCHAR(2000) NULL,
    
        [CreatedAt] NVARCHAR(50) NOT NULL,
    
        CONSTRAINT [FK_RecommendationLogs_Customers_CustomerId]
            FOREIGN KEY ([CustomerId])
            REFERENCES [Customers] ([CustomerId])
            ON DELETE CASCADE,
    
        CONSTRAINT [FK_RecommendationLogs_Vehicles_VehicleId]
            FOREIGN KEY ([VehicleId])
            REFERENCES [Vehicles] ([VehicleId])
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_GoogleSubject' AND object_id = OBJECT_ID(N'dbo.Users'))
BEGIN
    CREATE INDEX [IX_Users_GoogleSubject] ON dbo.[Users] ([GoogleSubject]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EmailOtps_Email_Purpose_CreatedAt' AND object_id = OBJECT_ID(N'dbo.EmailOtps'))
BEGIN
    CREATE INDEX [IX_EmailOtps_Email_Purpose_CreatedAt] ON dbo.[EmailOtps] ([Email], [Purpose], [CreatedAt]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Vehicles_LicensePlate' AND object_id = OBJECT_ID(N'dbo.Vehicles'))
BEGIN
    CREATE UNIQUE INDEX [IX_Vehicles_LicensePlate] ON dbo.[Vehicles] ([LicensePlate]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Bookings_CustomerId' AND object_id = OBJECT_ID(N'dbo.Bookings'))
BEGIN
    CREATE INDEX [IX_Bookings_CustomerId] ON dbo.[Bookings] ([CustomerId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Bookings_VehicleTypeId' AND object_id = OBJECT_ID(N'dbo.Bookings'))
BEGIN
    CREATE INDEX [IX_Bookings_VehicleTypeId] ON dbo.[Bookings] ([VehicleTypeId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Bookings_AssignedVehicleId' AND object_id = OBJECT_ID(N'dbo.Bookings'))
BEGIN
    CREATE INDEX [IX_Bookings_AssignedVehicleId] ON dbo.[Bookings] ([AssignedVehicleId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TripAssignments_BookingId' AND object_id = OBJECT_ID(N'dbo.TripAssignments'))
BEGIN
    CREATE UNIQUE INDEX [IX_TripAssignments_BookingId] ON dbo.[TripAssignments] ([BookingId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TripAssignments_DriverId' AND object_id = OBJECT_ID(N'dbo.TripAssignments'))
BEGIN
    CREATE INDEX [IX_TripAssignments_DriverId] ON dbo.[TripAssignments] ([DriverId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TripAssignments_VehicleId' AND object_id = OBJECT_ID(N'dbo.TripAssignments'))
BEGIN
    CREATE INDEX [IX_TripAssignments_VehicleId] ON dbo.[TripAssignments] ([VehicleId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BookingStatusHistory_BookingId' AND object_id = OBJECT_ID(N'dbo.BookingStatusHistory'))
BEGIN
    CREATE INDEX [IX_BookingStatusHistory_BookingId] ON dbo.[BookingStatusHistory] ([BookingId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payments_BookingId' AND object_id = OBJECT_ID(N'dbo.Payments'))
BEGIN
    CREATE INDEX [IX_Payments_BookingId] ON dbo.[Payments] ([BookingId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Contracts_BookingId' AND object_id = OBJECT_ID(N'dbo.Contracts'))
BEGIN
    CREATE UNIQUE INDEX [IX_Contracts_BookingId] ON dbo.[Contracts] ([BookingId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Contracts_ContractNumber' AND object_id = OBJECT_ID(N'dbo.Contracts'))
BEGIN
    CREATE UNIQUE INDEX [IX_Contracts_ContractNumber] ON dbo.[Contracts] ([ContractNumber]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Reviews_BookingId' AND object_id = OBJECT_ID(N'dbo.Reviews'))
BEGIN
    CREATE UNIQUE INDEX [IX_Reviews_BookingId] ON dbo.[Reviews] ([BookingId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VehicleInspections_BookingId' AND object_id = OBJECT_ID(N'dbo.VehicleInspections'))
BEGIN
    CREATE INDEX [IX_VehicleInspections_BookingId] ON dbo.[VehicleInspections] ([BookingId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VehicleInspections_VehicleId' AND object_id = OBJECT_ID(N'dbo.VehicleInspections'))
BEGIN
    CREATE INDEX [IX_VehicleInspections_VehicleId] ON dbo.[VehicleInspections] ([VehicleId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BookingFees_BookingId' AND object_id = OBJECT_ID(N'dbo.BookingFees'))
BEGIN
    CREATE INDEX [IX_BookingFees_BookingId] ON dbo.[BookingFees] ([BookingId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MaintenanceRecords_VehicleId' AND object_id = OBJECT_ID(N'dbo.MaintenanceRecords'))
BEGIN
    CREATE INDEX [IX_MaintenanceRecords_VehicleId] ON dbo.[MaintenanceRecords] ([VehicleId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IncidentReports_BookingId' AND object_id = OBJECT_ID(N'dbo.IncidentReports'))
BEGIN
    CREATE INDEX [IX_IncidentReports_BookingId] ON dbo.[IncidentReports] ([BookingId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IncidentReports_DriverId' AND object_id = OBJECT_ID(N'dbo.IncidentReports'))
BEGIN
    CREATE INDEX [IX_IncidentReports_DriverId] ON dbo.[IncidentReports] ([DriverId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IncidentReports_AssignmentId' AND object_id = OBJECT_ID(N'dbo.IncidentReports'))
BEGIN
    CREATE INDEX [IX_IncidentReports_AssignmentId] ON dbo.[IncidentReports] ([AssignmentId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notifications_UserId' AND object_id = OBJECT_ID(N'dbo.Notifications'))
BEGIN
    CREATE INDEX [IX_Notifications_UserId] ON dbo.[Notifications] ([UserId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VehicleStatusHistory_VehicleId' AND object_id = OBJECT_ID(N'dbo.VehicleStatusHistory'))
BEGIN
    CREATE INDEX [IX_VehicleStatusHistory_VehicleId] ON dbo.[VehicleStatusHistory] ([VehicleId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VehicleStatusHistory_ChangedAt' AND object_id = OBJECT_ID(N'dbo.VehicleStatusHistory'))
BEGIN
    CREATE INDEX [IX_VehicleStatusHistory_ChangedAt] ON dbo.[VehicleStatusHistory] ([ChangedAt]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RecommendationLogs_CustomerId' AND object_id = OBJECT_ID(N'dbo.RecommendationLogs'))
BEGIN
    CREATE INDEX [IX_RecommendationLogs_CustomerId] ON dbo.[RecommendationLogs] ([CustomerId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RecommendationLogs_VehicleId' AND object_id = OBJECT_ID(N'dbo.RecommendationLogs'))
BEGIN
    CREATE INDEX [IX_RecommendationLogs_VehicleId] ON dbo.[RecommendationLogs] ([VehicleId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RecommendationLogs_CreatedAt' AND object_id = OBJECT_ID(N'dbo.RecommendationLogs'))
BEGIN
    CREATE INDEX [IX_RecommendationLogs_CreatedAt] ON dbo.[RecommendationLogs] ([CreatedAt]);
END;
GO

-- ============================================================================
-- CHECK: total user tables
-- ============================================================================
SELECT COUNT(*) AS TotalTables
FROM sys.tables
WHERE schema_id = SCHEMA_ID(N'dbo')
  AND is_ms_shipped = 0;
GO

-- ============================================================================
-- CHECK: list all user tables
-- ============================================================================
SELECT name AS TableName
FROM sys.tables
WHERE schema_id = SCHEMA_ID(N'dbo')
  AND is_ms_shipped = 0
ORDER BY name;
GO

GO
USE [CarRentalSystem];
GO

/* ============================================================================
   DATA EXPANSION - Car Rental + Recommendation
   Keeps the existing 20-table schema and adds only the agreed extensions.
   ============================================================================ */

/* 1. Customers */
IF COL_LENGTH(N'dbo.Customers', N'DriverLicenseNumber') IS NULL ALTER TABLE dbo.Customers ADD DriverLicenseNumber NVARCHAR(100) NULL;
IF COL_LENGTH(N'dbo.Customers', N'DriverLicenseClass') IS NULL ALTER TABLE dbo.Customers ADD DriverLicenseClass NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.Customers', N'DriverLicenseExpiry') IS NULL ALTER TABLE dbo.Customers ADD DriverLicenseExpiry NVARCHAR(20) NULL;
IF COL_LENGTH(N'dbo.Customers', N'VerificationStatus') IS NULL ALTER TABLE dbo.Customers ADD VerificationStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_Customers_VerificationStatus DEFAULT 'Pending';
IF COL_LENGTH(N'dbo.Customers', N'MembershipTier') IS NULL ALTER TABLE dbo.Customers ADD MembershipTier NVARCHAR(50) NOT NULL CONSTRAINT DF_Customers_MembershipTier DEFAULT 'Standard';
IF COL_LENGTH(N'dbo.Customers', N'JoinedAt') IS NULL ALTER TABLE dbo.Customers ADD JoinedAt NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.Customers', N'Occupation') IS NULL ALTER TABLE dbo.Customers ADD Occupation NVARCHAR(150) NULL;
IF COL_LENGTH(N'dbo.Customers', N'IncomeRange') IS NULL ALTER TABLE dbo.Customers ADD IncomeRange NVARCHAR(100) NULL;
IF COL_LENGTH(N'dbo.Customers', N'FamilySize') IS NULL ALTER TABLE dbo.Customers ADD FamilySize INT NULL;
IF COL_LENGTH(N'dbo.Customers', N'NumberOfChildren') IS NULL ALTER TABLE dbo.Customers ADD NumberOfChildren INT NULL;
IF COL_LENGTH(N'dbo.Customers', N'CityType') IS NULL ALTER TABLE dbo.Customers ADD CityType NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.Customers', N'AnnualMileage') IS NULL ALTER TABLE dbo.Customers ADD AnnualMileage INT NULL;
IF COL_LENGTH(N'dbo.Customers', N'CommuteKmPerDay') IS NULL ALTER TABLE dbo.Customers ADD CommuteKmPerDay DECIMAL(10,2) NULL;
IF COL_LENGTH(N'dbo.Customers', N'RentalPurpose') IS NULL ALTER TABLE dbo.Customers ADD RentalPurpose NVARCHAR(100) NULL;
IF COL_LENGTH(N'dbo.Customers', N'PreferredVehicleType') IS NULL ALTER TABLE dbo.Customers ADD PreferredVehicleType NVARCHAR(100) NULL;
IF COL_LENGTH(N'dbo.Customers', N'PreferredFuelType') IS NULL ALTER TABLE dbo.Customers ADD PreferredFuelType NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.Customers', N'PreferredTransmission') IS NULL ALTER TABLE dbo.Customers ADD PreferredTransmission NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.Customers', N'PreferredSeats') IS NULL ALTER TABLE dbo.Customers ADD PreferredSeats INT NULL;
IF COL_LENGTH(N'dbo.Customers', N'BudgetMin') IS NULL ALTER TABLE dbo.Customers ADD BudgetMin DECIMAL(18,2) NULL;
IF COL_LENGTH(N'dbo.Customers', N'BudgetMax') IS NULL ALTER TABLE dbo.Customers ADD BudgetMax DECIMAL(18,2) NULL;
IF COL_LENGTH(N'dbo.Customers', N'PreferencesText') IS NULL ALTER TABLE dbo.Customers ADD PreferencesText NVARCHAR(2000) NULL;
IF COL_LENGTH(N'dbo.Customers', N'AvatarUrl') IS NULL ALTER TABLE dbo.Customers ADD AvatarUrl NVARCHAR(1000) NULL;
GO

/* 2. Drivers */
IF COL_LENGTH(N'dbo.Drivers', N'LicenseClass') IS NULL ALTER TABLE dbo.Drivers ADD LicenseClass NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.Drivers', N'LicenseIssueDate') IS NULL ALTER TABLE dbo.Drivers ADD LicenseIssueDate NVARCHAR(20) NULL;
IF COL_LENGTH(N'dbo.Drivers', N'IdNumber') IS NULL ALTER TABLE dbo.Drivers ADD IdNumber NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.Drivers', N'DateOfBirth') IS NULL ALTER TABLE dbo.Drivers ADD DateOfBirth NVARCHAR(20) NULL;
IF COL_LENGTH(N'dbo.Drivers', N'Address') IS NULL ALTER TABLE dbo.Drivers ADD Address NVARCHAR(1000) NULL;
IF COL_LENGTH(N'dbo.Drivers', N'AvatarUrl') IS NULL ALTER TABLE dbo.Drivers ADD AvatarUrl NVARCHAR(1000) NULL;
IF COL_LENGTH(N'dbo.Drivers', N'ExperienceYears') IS NULL ALTER TABLE dbo.Drivers ADD ExperienceYears DECIMAL(5,2) NULL;
IF COL_LENGTH(N'dbo.Drivers', N'OnTimeRate') IS NULL ALTER TABLE dbo.Drivers ADD OnTimeRate DECIMAL(5,2) NULL;
IF COL_LENGTH(N'dbo.Drivers', N'TotalDistanceKm') IS NULL ALTER TABLE dbo.Drivers ADD TotalDistanceKm DECIMAL(12,2) NULL;
IF COL_LENGTH(N'dbo.Drivers', N'VerificationStatus') IS NULL ALTER TABLE dbo.Drivers ADD VerificationStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_Drivers_VerificationStatus DEFAULT 'Pending';
GO

/* 3. Vehicles */
IF COL_LENGTH(N'dbo.Vehicles', N'Transmission') IS NULL ALTER TABLE dbo.Vehicles ADD Transmission NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.Vehicles', N'FuelType') IS NULL ALTER TABLE dbo.Vehicles ADD FuelType NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.Vehicles', N'SeatCapacity') IS NULL ALTER TABLE dbo.Vehicles ADD SeatCapacity INT NULL;
IF COL_LENGTH(N'dbo.Vehicles', N'TrunkCapacity') IS NULL ALTER TABLE dbo.Vehicles ADD TrunkCapacity DECIMAL(10,2) NULL;
IF COL_LENGTH(N'dbo.Vehicles', N'Engine') IS NULL ALTER TABLE dbo.Vehicles ADD Engine NVARCHAR(100) NULL;
IF COL_LENGTH(N'dbo.Vehicles', N'Horsepower') IS NULL ALTER TABLE dbo.Vehicles ADD Horsepower INT NULL;
IF COL_LENGTH(N'dbo.Vehicles', N'FuelConsumption') IS NULL ALTER TABLE dbo.Vehicles ADD FuelConsumption DECIMAL(10,2) NULL;
IF COL_LENGTH(N'dbo.Vehicles', N'SafetyScore') IS NULL ALTER TABLE dbo.Vehicles ADD SafetyScore DECIMAL(5,2) NULL;
IF COL_LENGTH(N'dbo.Vehicles', N'DailyPrice') IS NULL ALTER TABLE dbo.Vehicles ADD DailyPrice DECIMAL(18,2) NULL;
IF COL_LENGTH(N'dbo.Vehicles', N'WeeklyPrice') IS NULL ALTER TABLE dbo.Vehicles ADD WeeklyPrice DECIMAL(18,2) NULL;
IF COL_LENGTH(N'dbo.Vehicles', N'MonthlyPrice') IS NULL ALTER TABLE dbo.Vehicles ADD MonthlyPrice DECIMAL(18,2) NULL;
IF COL_LENGTH(N'dbo.Vehicles', N'ConditionStatus') IS NULL ALTER TABLE dbo.Vehicles ADD ConditionStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_Vehicles_ConditionStatus DEFAULT 'Good';
IF COL_LENGTH(N'dbo.Vehicles', N'Notes') IS NULL ALTER TABLE dbo.Vehicles ADD Notes NVARCHAR(2000) NULL;
IF COL_LENGTH(N'dbo.Vehicles', N'LastMaintenanceDate') IS NULL ALTER TABLE dbo.Vehicles ADD LastMaintenanceDate NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.Vehicles', N'LastMaintenanceOdometer') IS NULL ALTER TABLE dbo.Vehicles ADD LastMaintenanceOdometer INT NULL;
IF COL_LENGTH(N'dbo.Vehicles', N'MaintenanceIntervalKm') IS NULL ALTER TABLE dbo.Vehicles ADD MaintenanceIntervalKm INT NULL;
IF COL_LENGTH(N'dbo.Vehicles', N'MaintenanceIntervalDays') IS NULL ALTER TABLE dbo.Vehicles ADD MaintenanceIntervalDays INT NULL;
IF COL_LENGTH(N'dbo.Vehicles', N'NextMaintenanceDate') IS NULL ALTER TABLE dbo.Vehicles ADD NextMaintenanceDate NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.Vehicles', N'NextMaintenanceOdometer') IS NULL ALTER TABLE dbo.Vehicles ADD NextMaintenanceOdometer INT NULL;
GO

/* 4. Booking: customer-selected requested driver */
IF COL_LENGTH(N'dbo.Bookings', N'RequestedDriverId') IS NULL ALTER TABLE dbo.Bookings ADD RequestedDriverId INT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_Bookings_Drivers_RequestedDriverId' AND parent_object_id=OBJECT_ID(N'dbo.Bookings'))
BEGIN
    ALTER TABLE dbo.Bookings ADD CONSTRAINT FK_Bookings_Drivers_RequestedDriverId
        FOREIGN KEY (RequestedDriverId) REFERENCES dbo.Drivers(DriverId);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Bookings_RequestedDriverId' AND object_id=OBJECT_ID(N'dbo.Bookings'))
    CREATE INDEX IX_Bookings_RequestedDriverId ON dbo.Bookings(RequestedDriverId);
GO

/* 5. Reviews: explicit vehicle rating + detailed ratings */
IF COL_LENGTH(N'dbo.Reviews', N'VehicleId') IS NULL ALTER TABLE dbo.Reviews ADD VehicleId INT NULL;
IF COL_LENGTH(N'dbo.Reviews', N'ComfortRating') IS NULL ALTER TABLE dbo.Reviews ADD ComfortRating INT NULL;
IF COL_LENGTH(N'dbo.Reviews', N'CleanlinessRating') IS NULL ALTER TABLE dbo.Reviews ADD CleanlinessRating INT NULL;
IF COL_LENGTH(N'dbo.Reviews', N'PerformanceRating') IS NULL ALTER TABLE dbo.Reviews ADD PerformanceRating INT NULL;
IF COL_LENGTH(N'dbo.Reviews', N'ValueRating') IS NULL ALTER TABLE dbo.Reviews ADD ValueRating INT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_Reviews_Vehicles_VehicleId' AND parent_object_id=OBJECT_ID(N'dbo.Reviews'))
BEGIN
    ALTER TABLE dbo.Reviews ADD CONSTRAINT FK_Reviews_Vehicles_VehicleId
        FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles(VehicleId);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Reviews_VehicleId' AND object_id=OBJECT_ID(N'dbo.Reviews'))
    CREATE INDEX IX_Reviews_VehicleId ON dbo.Reviews(VehicleId);
GO

/* 6. Maintenance: thresholds by ODO/date */
IF COL_LENGTH(N'dbo.MaintenanceRecords', N'ScheduledOdometer') IS NULL ALTER TABLE dbo.MaintenanceRecords ADD ScheduledOdometer INT NULL;
IF COL_LENGTH(N'dbo.MaintenanceRecords', N'NextMaintenanceDate') IS NULL ALTER TABLE dbo.MaintenanceRecords ADD NextMaintenanceDate NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.MaintenanceRecords', N'NextMaintenanceOdometer') IS NULL ALTER TABLE dbo.MaintenanceRecords ADD NextMaintenanceOdometer INT NULL;
IF COL_LENGTH(N'dbo.MaintenanceRecords', N'Status') IS NULL ALTER TABLE dbo.MaintenanceRecords ADD Status NVARCHAR(50) NOT NULL CONSTRAINT DF_MaintenanceRecords_Status DEFAULT 'Completed';
GO

/* 7. VehicleImages */
IF OBJECT_ID(N'dbo.VehicleImages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VehicleImages (
        VehicleImageId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_VehicleImages PRIMARY KEY,
        VehicleId INT NOT NULL,
        ImageUrl NVARCHAR(1000) NOT NULL,
        ImageType NVARCHAR(50) NULL,
        IsPrimary INT NOT NULL DEFAULT 0,
        DisplayOrder INT NOT NULL DEFAULT 0,
        CreatedAt NVARCHAR(50) NOT NULL,
        CONSTRAINT FK_VehicleImages_Vehicles_VehicleId
            FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles(VehicleId) ON DELETE CASCADE
    );
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_VehicleImages_VehicleId' AND object_id=OBJECT_ID(N'dbo.VehicleImages'))
    CREATE INDEX IX_VehicleImages_VehicleId ON dbo.VehicleImages(VehicleId);
GO

/* 8. CustomerVehicleInteractions */
IF OBJECT_ID(N'dbo.CustomerVehicleInteractions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerVehicleInteractions (
        InteractionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CustomerVehicleInteractions PRIMARY KEY,
        CustomerId INT NOT NULL,
        VehicleId INT NOT NULL,
        InteractionType NVARCHAR(50) NOT NULL,
        CreatedAt NVARCHAR(50) NOT NULL,
        CONSTRAINT FK_CustomerVehicleInteractions_Customers_CustomerId
            FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerId) ON DELETE CASCADE,
        CONSTRAINT FK_CustomerVehicleInteractions_Vehicles_VehicleId
            FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles(VehicleId) ON DELETE CASCADE
    );
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_CustomerVehicleInteractions_CustomerId' AND object_id=OBJECT_ID(N'dbo.CustomerVehicleInteractions'))
    CREATE INDEX IX_CustomerVehicleInteractions_CustomerId ON dbo.CustomerVehicleInteractions(CustomerId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_CustomerVehicleInteractions_VehicleId' AND object_id=OBJECT_ID(N'dbo.CustomerVehicleInteractions'))
    CREATE INDEX IX_CustomerVehicleInteractions_VehicleId ON dbo.CustomerVehicleInteractions(VehicleId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_CustomerVehicleInteractions_Type_CreatedAt' AND object_id=OBJECT_ID(N'dbo.CustomerVehicleInteractions'))
    CREATE INDEX IX_CustomerVehicleInteractions_Type_CreatedAt ON dbo.CustomerVehicleInteractions(InteractionType, CreatedAt);
GO

/* Maintenance rule used by the application:
   A vehicle is due when CurrentKm >= NextMaintenanceOdometer
   OR current date >= NextMaintenanceDate.
*/
SELECT name AS TableName
FROM sys.tables
WHERE schema_id = SCHEMA_ID(N'dbo') AND is_ms_shipped = 0
ORDER BY name;
GO
