/*
    Car Rental System - Seed Data
    Chay sau 02_CreateTables.sql

    Mat khau mau cho tat ca tai khoan: Password123!
*/

USE CarRentalDB;
GO

-- Hash placeholder (se thay khi tich hop Backend Auth)
DECLARE @DefaultPasswordHash NVARCHAR(256) = N'$2a$11$3m3UqMzFHr5xfsLvPE47R.4tD/uIDb7SMZ/hTQFl1ZrGUl9zUdN.a';

/* Roles */
INSERT INTO Roles (RoleName, Description) VALUES
(N'Admin',      N'Quan tri he thong'),
(N'Dispatcher', N'Dieu phoi chuyen di'),
(N'Customer',   N'Khach hang thue xe'),
(N'Driver',     N'Tai xe');

/* Users - Admin, Dispatcher */
INSERT INTO Users (Email, PasswordHash, FullName, Phone, RoleId) VALUES
(N'admin@carrental.vn',      @DefaultPasswordHash, N'Nguyen Van Admin',      N'0901000001', 1),
(N'dispatcher@carrental.vn', @DefaultPasswordHash, N'Tran Thi Dieu Phoi',    N'0901000002', 2);

/* Users - Customers */
INSERT INTO Users (Email, PasswordHash, FullName, Phone, RoleId) VALUES
(N'customer1@gmail.com', @DefaultPasswordHash, N'Le Van Khach',   N'0912000001', 3),
(N'customer2@gmail.com', @DefaultPasswordHash, N'Pham Thi Mai',   N'0912000002', 3);

INSERT INTO Customers (CustomerId, Address, IdNumber, DateOfBirth) VALUES
(3, N'123 Nguyen Hue, Q1, TP.HCM', N'079123456789', '1995-03-15'),
(4, N'456 Le Loi, Q3, TP.HCM',     N'079987654321', '1990-08-22');

/* Users - Drivers */
INSERT INTO Users (Email, PasswordHash, FullName, Phone, RoleId) VALUES
(N'driver1@carrental.vn', @DefaultPasswordHash, N'Hoang Van Tai',  N'0923000001', 4),
(N'driver2@carrental.vn', @DefaultPasswordHash, N'Vo Thi Lai',    N'0923000002', 4),
(N'driver3@carrental.vn', @DefaultPasswordHash, N'Dang Quoc Hung', N'0923000003', 4);

INSERT INTO Drivers (DriverId, LicenseNumber, LicenseExpiry, Status, AverageRating, TotalTrips) VALUES
(5, N'B2-123456789', '2028-12-31', N'Available', 4.80, 120),
(6, N'B2-987654321', '2027-06-30', N'Available', 4.50, 85),
(7, N'B2-555666777', '2029-01-15', N'Offline',   4.90, 200);

/* Vehicle Types */
INSERT INTO VehicleTypes (TypeName, SeatCapacity, PricePerDay, PricePerKm, Description) VALUES
(N'4 cho - Sedan',  4,  800000,  12000, N'Xe sedan 4 cho, phu hop di noi thanh'),
(N'7 cho - SUV',    7, 1200000,  15000, N'SUV 7 cho, phu hop gia dinh'),
(N'16 cho - Van',  16, 2500000,  20000, N'Xe van 16 cho, phu hop tour nhom'),
(N'Limousine 9 cho', 9, 3500000, 25000, N'Xe limousine cao cap');

/* Vehicles */
INSERT INTO Vehicles (TypeId, LicensePlate, Brand, Model, [Year], Color, Status, CurrentKm) VALUES
(1, N'51A-12345', N'Toyota',    N'Vios',       2022, N'Trắng',  N'Available', 45000),
(1, N'51B-67890', N'Hyundai',   N'Accent',     2023, N'Bạc',    N'Available', 22000),
(2, N'51C-11111', N'Toyota',    N'Fortuner',   2021, N'Đen',    N'Available', 78000),
(2, N'51D-22222', N'Ford',      N'Everest',    2022, N'Xám',    N'Available', 55000),
(3, N'51E-33333', N'Hyundai',   N'Solati',     2020, N'Trắng',  N'Available', 95000),
(4, N'51F-44444', N'Ford',      N'Transit',    2023, N'Đen',    N'Maintenance', 12000);

/* Sample Bookings */
INSERT INTO Bookings (
    CustomerId, VehicleTypeId, PickupAddress, DropoffAddress,
    StartDate, EndDate, EstimatedDistance, TotalAmount, Status, Notes
) VALUES
(3, 2,
 N'San bay Tan Son Nhat, TP.HCM',
 N'Da Lat, Lam Dong',
 '2026-09-01 08:00:00', '2026-09-03 18:00:00',
 300.00, 6600000, N'Pending',
 N'Thu xe di du lich Da Lat 3 ngay'),

(4, 1,
 N'123 Nguyen Hue, Q1, TP.HCM',
 N'Vung Tau, Ba Ria-Vung Tau',
 '2026-08-25 06:00:00', '2026-08-25 22:00:00',
 120.00, 2240000, N'Confirmed',
 N'Di Vung Tau trong ngay');

/* Trip assignment for confirmed booking */
INSERT INTO TripAssignments (BookingId, DriverId, VehicleId, AssignedBy, Status) VALUES
(2, 5, 1, 2, N'Assigned');

UPDATE Bookings SET Status = N'Assigned' WHERE BookingId = 2;

INSERT INTO BookingStatusHistory (BookingId, OldStatus, NewStatus, ChangedBy, Note) VALUES
(1, NULL,          N'Pending',   3, N'Khach tao don dat xe'),
(2, NULL,          N'Pending',   4, N'Khach tao don dat xe'),
(2, N'Pending',    N'Confirmed', 2, N'Dieu phoi xac nhan don'),
(2, N'Confirmed',  N'Assigned',  2, N'Phan cong tai xe Hoang Van Tai');

/* Sample Payment */
INSERT INTO Payments (BookingId, Amount, Method, Status, TransactionRef, PaidAt) VALUES
(2, 2240000, N'BankTransfer', N'Paid', N'TXN-20260825-001', '2026-08-24 15:30:00');

GO
