/*
    Car Rental System - Tables
    Chay sau 01_CreateDatabase.sql
*/

USE CarRentalDB;
GO

/* =========================
   1. VAI TRO & NGUOI DUNG
   ========================= */

CREATE TABLE Roles (
    RoleId      INT IDENTITY(1,1) PRIMARY KEY,
    RoleName    NVARCHAR(50)  NOT NULL UNIQUE,
    Description NVARCHAR(200) NULL
);

CREATE TABLE Users (
    UserId       INT IDENTITY(1,1) PRIMARY KEY,
    Email        NVARCHAR(100) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(256) NOT NULL,
    FullName     NVARCHAR(100) NOT NULL,
    Phone        NVARCHAR(20)  NULL,
    RoleId       INT           NOT NULL,
    IsActive     BIT           NOT NULL DEFAULT 1,
    CreatedAt    DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt    DATETIME2     NULL,
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
);

CREATE TABLE Customers (
    CustomerId  INT PRIMARY KEY,
    Address     NVARCHAR(255) NULL,
    IdNumber    NVARCHAR(20)  NULL,
    DateOfBirth DATE          NULL,
    CONSTRAINT FK_Customers_Users FOREIGN KEY (CustomerId) REFERENCES Users(UserId)
);

CREATE TABLE Drivers (
    DriverId        INT PRIMARY KEY,
    LicenseNumber   NVARCHAR(30)  NOT NULL UNIQUE,
    LicenseExpiry   DATE          NOT NULL,
    Status          NVARCHAR(20)  NOT NULL DEFAULT N'Offline',
    AverageRating   DECIMAL(3,2)  NOT NULL DEFAULT 0,
    TotalTrips      INT           NOT NULL DEFAULT 0,
    CONSTRAINT FK_Drivers_Users FOREIGN KEY (DriverId) REFERENCES Users(UserId),
    CONSTRAINT CK_Drivers_Status CHECK (Status IN (N'Available', N'Busy', N'Offline'))
);

/* =========================
   2. XE & LOAI XE
   ========================= */

CREATE TABLE VehicleTypes (
    TypeId        INT IDENTITY(1,1) PRIMARY KEY,
    TypeName      NVARCHAR(50)   NOT NULL UNIQUE,
    SeatCapacity  INT            NOT NULL,
    PricePerDay   DECIMAL(18,2)  NOT NULL,
    PricePerKm    DECIMAL(18,2)  NOT NULL DEFAULT 0,
    DriverFeePerDay DECIMAL(18,2) NULL,
    SelfDrivePricePerDay DECIMAL(18,2) NULL,
    SelfDriveIncludedKmPerDay DECIMAL(10,2) NULL,
    SelfDriveExtraKmPrice DECIMAL(18,2) NULL,
    WithDriverDepositAmount DECIMAL(18,2) NULL,
    SelfDriveDepositAmount DECIMAL(18,2) NULL,
    Description   NVARCHAR(500)  NULL,
    ImageUrl      NVARCHAR(500)  NULL,
    IsActive      BIT            NOT NULL DEFAULT 1,
    CONSTRAINT CK_VehicleTypes_SeatCapacity CHECK (SeatCapacity > 0),
    CONSTRAINT CK_VehicleTypes_PricePerDay CHECK (PricePerDay >= 0)
);

CREATE TABLE Vehicles (
    VehicleId     INT IDENTITY(1,1) PRIMARY KEY,
    TypeId        INT            NOT NULL,
    LicensePlate  NVARCHAR(20)   NOT NULL UNIQUE,
    Brand         NVARCHAR(50)   NOT NULL,
    Model         NVARCHAR(50)   NOT NULL,
    [Year]        INT            NOT NULL,
    Color         NVARCHAR(30)   NULL,
    Status        NVARCHAR(20)   NOT NULL DEFAULT N'Available',
    CurrentKm     INT            NOT NULL DEFAULT 0,
    CreatedAt     DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Vehicles_VehicleTypes FOREIGN KEY (TypeId) REFERENCES VehicleTypes(TypeId),
    CONSTRAINT CK_Vehicles_Status CHECK (Status IN (N'Available', N'Rented', N'Maintenance', N'Inactive')),
    CONSTRAINT CK_Vehicles_Year CHECK ([Year] >= 1990 AND [Year] <= 2100)
);

/* =========================
   3. DAT XE & PHAN CONG
   ========================= */

CREATE TABLE Bookings (
    BookingId         INT IDENTITY(1,1) PRIMARY KEY,
    CustomerId        INT            NOT NULL,
    VehicleTypeId     INT            NOT NULL,
    PickupAddress     NVARCHAR(255)  NOT NULL,
    DropoffAddress    NVARCHAR(255)  NOT NULL,
    PickupLat         DECIMAL(10,7)  NULL,
    PickupLng         DECIMAL(10,7)  NULL,
    DropoffLat        DECIMAL(10,7)  NULL,
    DropoffLng        DECIMAL(10,7)  NULL,
    StartDate         DATETIME2      NOT NULL,
    EndDate           DATETIME2      NOT NULL,
    EstimatedDistance DECIMAL(10,2)  NULL,
    TotalAmount       DECIMAL(18,2)  NOT NULL DEFAULT 0,
    QuotedPricePerDay DECIMAL(18,2)  NULL,
    QuotedPricePerKm  DECIMAL(18,2)  NULL,
    QuotedDays        INT            NULL,
    QuotedDriverFeePerDay DECIMAL(18,2) NULL,
    QuotedSelfDriveIncludedKmPerDay DECIMAL(10,2) NULL,
    QuotedSelfDriveExtraKmPrice DECIMAL(18,2) NULL,
    QuotedDepositAmount DECIMAL(18,2) NULL,
    FinalAmount       DECIMAL(18,2)  NULL,
    Status            NVARCHAR(30)   NOT NULL DEFAULT N'Pending',
    RentalMode        NVARCHAR(20)   NOT NULL DEFAULT N'WithDriver',
    AssignedVehicleId INT            NULL,
    Notes             NVARCHAR(500)  NULL,
    CreatedAt         DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2      NULL,
    CONSTRAINT FK_Bookings_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
    CONSTRAINT FK_Bookings_VehicleTypes FOREIGN KEY (VehicleTypeId) REFERENCES VehicleTypes(TypeId),
    CONSTRAINT FK_Bookings_AssignedVehicle FOREIGN KEY (AssignedVehicleId) REFERENCES Vehicles(VehicleId),
    CONSTRAINT CK_Bookings_Status CHECK (Status IN (
        N'Pending', N'Confirmed', N'Assigned', N'InProgress', N'Completed', N'Cancelled'
    )),
    CONSTRAINT CK_Bookings_RentalMode CHECK (RentalMode IN (N'WithDriver', N'SelfDrive')),
    CONSTRAINT CK_Bookings_Dates CHECK (EndDate > StartDate),
    CONSTRAINT CK_Bookings_TotalAmount CHECK (TotalAmount >= 0)
);

CREATE TABLE TripAssignments (
    AssignmentId INT IDENTITY(1,1) PRIMARY KEY,
    BookingId    INT           NOT NULL UNIQUE,
    DriverId     INT           NOT NULL,
    VehicleId    INT           NOT NULL,
    AssignedBy   INT           NOT NULL,
    AssignedAt   DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    Status       NVARCHAR(20)  NOT NULL DEFAULT N'Assigned',
    StartedAt    DATETIME2     NULL,
    CompletedAt  DATETIME2     NULL,
    CONSTRAINT FK_TripAssignments_Bookings FOREIGN KEY (BookingId) REFERENCES Bookings(BookingId),
    CONSTRAINT FK_TripAssignments_Drivers FOREIGN KEY (DriverId) REFERENCES Drivers(DriverId),
    CONSTRAINT FK_TripAssignments_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(VehicleId),
    CONSTRAINT FK_TripAssignments_AssignedBy FOREIGN KEY (AssignedBy) REFERENCES Users(UserId),
    CONSTRAINT CK_TripAssignments_Status CHECK (Status IN (
        N'Assigned', N'Accepted', N'InProgress', N'Completed', N'Cancelled'
    ))
);

CREATE TABLE BookingStatusHistory (
    HistoryId  INT IDENTITY(1,1) PRIMARY KEY,
    BookingId  INT           NOT NULL,
    OldStatus  NVARCHAR(30)  NULL,
    NewStatus  NVARCHAR(30)  NOT NULL,
    ChangedBy  INT           NULL,
    Note       NVARCHAR(300) NULL,
    ChangedAt  DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_BookingStatusHistory_Bookings FOREIGN KEY (BookingId) REFERENCES Bookings(BookingId),
    CONSTRAINT FK_BookingStatusHistory_Users FOREIGN KEY (ChangedBy) REFERENCES Users(UserId)
);

/* =========================
   4. THANH TOAN & DANH GIA
   ========================= */

CREATE TABLE Payments (
    PaymentId      INT IDENTITY(1,1) PRIMARY KEY,
    BookingId      INT            NOT NULL,
    PaymentType    NVARCHAR(30)   NULL,
    Amount         DECIMAL(18,2)  NOT NULL,
    Method         NVARCHAR(30)   NOT NULL,
    Status         NVARCHAR(20)   NOT NULL DEFAULT N'Pending',
    TransactionRef NVARCHAR(100)  NULL,
    PaidAt         DATETIME2      NULL,
    CreatedAt      DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Payments_Bookings FOREIGN KEY (BookingId) REFERENCES Bookings(BookingId),
    CONSTRAINT CK_Payments_Amount CHECK (Amount >= 0),
    CONSTRAINT CK_Payments_Method CHECK (Method IN (N'Cash', N'BankTransfer', N'MoMo', N'VNPay')),
    CONSTRAINT CK_Payments_Status CHECK (Status IN (N'Pending', N'Paid', N'Failed', N'Refunded')),
    CONSTRAINT CK_Payments_PaymentType CHECK (PaymentType IS NULL OR PaymentType IN (N'Deposit', N'Balance', N'Refund'))
);

CREATE TABLE Reviews (
    ReviewId   INT IDENTITY(1,1) PRIMARY KEY,
    BookingId  INT           NOT NULL UNIQUE,
    CustomerId INT           NOT NULL,
    DriverId   INT           NOT NULL,
    Rating     TINYINT       NOT NULL,
    Comment    NVARCHAR(500) NULL,
    CreatedAt  DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Reviews_Bookings FOREIGN KEY (BookingId) REFERENCES Bookings(BookingId),
    CONSTRAINT FK_Reviews_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
    CONSTRAINT FK_Reviews_Drivers FOREIGN KEY (DriverId) REFERENCES Drivers(DriverId),
    CONSTRAINT CK_Reviews_Rating CHECK (Rating BETWEEN 1 AND 5)
);

/* =========================
   5. INDEX
   ========================= */

CREATE INDEX IX_Users_RoleId ON Users(RoleId);
CREATE INDEX IX_Vehicles_TypeId ON Vehicles(TypeId);
CREATE INDEX IX_Vehicles_Status ON Vehicles(Status);
CREATE INDEX IX_Drivers_Status ON Drivers(Status);
CREATE INDEX IX_Bookings_CustomerId ON Bookings(CustomerId);
CREATE INDEX IX_Bookings_Status ON Bookings(Status);
CREATE INDEX IX_Bookings_StartDate ON Bookings(StartDate);
CREATE INDEX IX_TripAssignments_DriverId ON TripAssignments(DriverId);
CREATE INDEX IX_TripAssignments_VehicleId ON TripAssignments(VehicleId);
CREATE INDEX IX_Payments_BookingId ON Payments(BookingId);

CREATE TABLE VehicleInspections (
    InspectionId    INT IDENTITY(1,1) PRIMARY KEY,
    BookingId       INT            NOT NULL,
    VehicleId       INT            NOT NULL,
    InspectionType  NVARCHAR(20)   NOT NULL,
    ActualAt        DATETIME2      NOT NULL,
    OdometerKm      DECIMAL(10,2)  NULL,
    FuelLevel       DECIMAL(5,2)   NULL,
    Condition       NVARCHAR(100)  NULL,
    Notes           NVARCHAR(500)  NULL,
    CreatedAt       DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_VehicleInspections_Bookings FOREIGN KEY (BookingId) REFERENCES Bookings(BookingId),
    CONSTRAINT FK_VehicleInspections_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(VehicleId),
    CONSTRAINT CK_VehicleInspections_Type CHECK (InspectionType IN (N'Handover', N'Return')),
    CONSTRAINT CK_VehicleInspections_Odometer CHECK (OdometerKm IS NULL OR OdometerKm >= 0),
    CONSTRAINT CK_VehicleInspections_Fuel CHECK (FuelLevel IS NULL OR (FuelLevel >= 0 AND FuelLevel <= 100))
);

CREATE INDEX IX_VehicleInspections_BookingId ON VehicleInspections(BookingId);
CREATE INDEX IX_VehicleInspections_VehicleId ON VehicleInspections(VehicleId);

CREATE TABLE BookingFees (
    FeeId       INT IDENTITY(1,1) PRIMARY KEY,
    BookingId   INT            NOT NULL,
    FeeType     NVARCHAR(30)   NOT NULL,
    Description NVARCHAR(300)  NULL,
    Amount      DECIMAL(18,2)  NOT NULL,
    CreatedAt   DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_BookingFees_Bookings FOREIGN KEY (BookingId) REFERENCES Bookings(BookingId),
    CONSTRAINT CK_BookingFees_Amount CHECK (Amount >= 0),
    CONSTRAINT CK_BookingFees_FeeType CHECK (FeeType IN (N'LateFee', N'ExtraKm', N'Fuel', N'Damage', N'Other'))
);

CREATE INDEX IX_BookingFees_BookingId ON BookingFees(BookingId);
GO
