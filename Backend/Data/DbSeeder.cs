using Backend.Constants;
using Backend.Entities;

namespace Backend.Data;

public static class DbSeeder
{
    private const string DefaultPasswordHash = "$2a$11$3m3UqMzFHr5xfsLvPE47R.4tD/uIDb7SMZ/hTQFl1ZrGUl9zUdN.a";

    public static void Seed(CarRentalDbContext db)
    {
        if (db.Roles.Any()) return;

        db.Roles.AddRange(
            new Role { RoleName = RoleNames.Admin, Description = "Quan tri he thong" },
            new Role { RoleName = RoleNames.Dispatcher, Description = "Dieu phoi chuyen di" },
            new Role { RoleName = RoleNames.Customer, Description = "Khach hang thue xe" },
            new Role { RoleName = RoleNames.Driver, Description = "Tai xe" });
        db.SaveChanges();

        var now = DateTime.UtcNow;

        db.Users.AddRange(
            new User { Email = "admin@carrental.vn", PasswordHash = DefaultPasswordHash, FullName = "Nguyen Van Admin", Phone = "0901000001", RoleId = 1, CreatedAt = now },
            new User { Email = "dispatcher@carrental.vn", PasswordHash = DefaultPasswordHash, FullName = "Tran Thi Dieu Phoi", Phone = "0901000002", RoleId = 2, CreatedAt = now },
            new User { Email = "customer1@gmail.com", PasswordHash = DefaultPasswordHash, FullName = "Le Van Khach", Phone = "0912000001", RoleId = 3, CreatedAt = now },
            new User { Email = "customer2@gmail.com", PasswordHash = DefaultPasswordHash, FullName = "Pham Thi Mai", Phone = "0912000002", RoleId = 3, CreatedAt = now },
            new User { Email = "driver1@carrental.vn", PasswordHash = DefaultPasswordHash, FullName = "Hoang Van Tai", Phone = "0923000001", RoleId = 4, CreatedAt = now },
            new User { Email = "driver2@carrental.vn", PasswordHash = DefaultPasswordHash, FullName = "Vo Thi Lai", Phone = "0923000002", RoleId = 4, CreatedAt = now },
            new User { Email = "driver3@carrental.vn", PasswordHash = DefaultPasswordHash, FullName = "Dang Quoc Hung", Phone = "0923000003", RoleId = 4, CreatedAt = now });
        db.SaveChanges();

        db.Customers.AddRange(
            new Customer { CustomerId = 3, Address = "123 Nguyen Hue, Q1, TP.HCM", IdNumber = "079123456789", DateOfBirth = new DateOnly(1995, 3, 15) },
            new Customer { CustomerId = 4, Address = "456 Le Loi, Q3, TP.HCM", IdNumber = "079987654321", DateOfBirth = new DateOnly(1990, 8, 22) });

        db.Drivers.AddRange(
            new Driver { DriverId = 5, LicenseNumber = "B2-123456789", LicenseExpiry = new DateOnly(2028, 12, 31), Status = DriverStatuses.Available, AverageRating = 4.80m, TotalTrips = 120 },
            new Driver { DriverId = 6, LicenseNumber = "B2-987654321", LicenseExpiry = new DateOnly(2027, 6, 30), Status = DriverStatuses.Available, AverageRating = 4.50m, TotalTrips = 85 },
            new Driver { DriverId = 7, LicenseNumber = "B2-555666777", LicenseExpiry = new DateOnly(2029, 1, 15), Status = DriverStatuses.Offline, AverageRating = 4.90m, TotalTrips = 200 });
        db.SaveChanges();

        db.VehicleTypes.AddRange(
            new VehicleType { TypeName = "4 cho - Sedan", SeatCapacity = 4, PricePerDay = 800000, PricePerKm = 12000, Description = "Xe sedan 4 cho, phu hop di noi thanh" },
            new VehicleType { TypeName = "7 cho - SUV", SeatCapacity = 7, PricePerDay = 1200000, PricePerKm = 15000, Description = "SUV 7 cho, phu hop gia dinh" },
            new VehicleType { TypeName = "16 cho - Van", SeatCapacity = 16, PricePerDay = 2500000, PricePerKm = 20000, Description = "Xe van 16 cho, phu hop tour nhom" },
            new VehicleType { TypeName = "Limousine 9 cho", SeatCapacity = 9, PricePerDay = 3500000, PricePerKm = 25000, Description = "Xe limousine cao cap" });
        db.SaveChanges();

        db.Vehicles.AddRange(
            new Vehicle { TypeId = 1, LicensePlate = "51A-12345", Brand = "Toyota", Model = "Vios", Year = 2022, Color = "Trang", Status = VehicleStatuses.Available, CurrentKm = 45000, CreatedAt = now },
            new Vehicle { TypeId = 1, LicensePlate = "51B-67890", Brand = "Hyundai", Model = "Accent", Year = 2023, Color = "Bac", Status = VehicleStatuses.Available, CurrentKm = 22000, CreatedAt = now },
            new Vehicle { TypeId = 2, LicensePlate = "51C-11111", Brand = "Toyota", Model = "Fortuner", Year = 2021, Color = "Den", Status = VehicleStatuses.Available, CurrentKm = 78000, CreatedAt = now },
            new Vehicle { TypeId = 2, LicensePlate = "51D-22222", Brand = "Ford", Model = "Everest", Year = 2022, Color = "Xam", Status = VehicleStatuses.Available, CurrentKm = 55000, CreatedAt = now },
            new Vehicle { TypeId = 3, LicensePlate = "51E-33333", Brand = "Hyundai", Model = "Solati", Year = 2020, Color = "Trang", Status = VehicleStatuses.Available, CurrentKm = 95000, CreatedAt = now },
            new Vehicle { TypeId = 4, LicensePlate = "51F-44444", Brand = "Ford", Model = "Transit", Year = 2023, Color = "Den", Status = VehicleStatuses.Maintenance, CurrentKm = 12000, CreatedAt = now });
        db.SaveChanges();

        db.Bookings.AddRange(
            new Booking
            {
                CustomerId = 3, VehicleTypeId = 2,
                PickupAddress = "San bay Tan Son Nhat, TP.HCM",
                DropoffAddress = "Da Lat, Lam Dong",
                StartDate = new DateTime(2026, 9, 1, 8, 0, 0),
                EndDate = new DateTime(2026, 9, 3, 18, 0, 0),
                EstimatedDistance = 300, TotalAmount = 6600000,
                Status = BookingStatuses.Pending, Notes = "Thu xe di du lich Da Lat 3 ngay", CreatedAt = now
            },
            new Booking
            {
                CustomerId = 4, VehicleTypeId = 1,
                PickupAddress = "123 Nguyen Hue, Q1, TP.HCM",
                DropoffAddress = "Vung Tau, Ba Ria-Vung Tau",
                StartDate = new DateTime(2026, 8, 25, 6, 0, 0),
                EndDate = new DateTime(2026, 8, 25, 22, 0, 0),
                EstimatedDistance = 120, TotalAmount = 2240000,
                Status = BookingStatuses.Assigned, Notes = "Di Vung Tau trong ngay", CreatedAt = now
            });
        db.SaveChanges();

        db.TripAssignments.Add(new TripAssignment
        {
            BookingId = 2, DriverId = 5, VehicleId = 1, AssignedBy = 2,
            AssignedAt = now, Status = TripAssignmentStatuses.Assigned
        });

        db.BookingStatusHistories.AddRange(
            new BookingStatusHistory { BookingId = 1, NewStatus = BookingStatuses.Pending, ChangedBy = 3, Note = "Khach tao don dat xe", ChangedAt = now },
            new BookingStatusHistory { BookingId = 2, NewStatus = BookingStatuses.Pending, ChangedBy = 4, Note = "Khach tao don dat xe", ChangedAt = now },
            new BookingStatusHistory { BookingId = 2, OldStatus = BookingStatuses.Pending, NewStatus = BookingStatuses.Confirmed, ChangedBy = 2, Note = "Dieu phoi xac nhan don", ChangedAt = now },
            new BookingStatusHistory { BookingId = 2, OldStatus = BookingStatuses.Confirmed, NewStatus = BookingStatuses.Assigned, ChangedBy = 2, Note = "Phan cong tai xe Hoang Van Tai", ChangedAt = now });

        db.Payments.Add(new Payment
        {
            BookingId = 2, Amount = 2240000, Method = "BankTransfer", Status = "Paid",
            TransactionRef = "TXN-20260825-001", PaidAt = new DateTime(2026, 8, 24, 15, 30, 0), CreatedAt = now
        });

        db.SaveChanges();
    }
}
