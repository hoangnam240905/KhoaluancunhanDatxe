using Backend.Constants;
using Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

/// <summary>
/// Append-only demo dataset for bảo vệ/demo. Does not replace Core <see cref="DbSeeder"/>.
/// Guard: skip if Bus type already exists. Never call this against live carrental.db.
/// </summary>
public static class DemoRichSeeder
{
    public const string BusTypeName = "29 chỗ - Bus";
    public const string MpvTypeName = "7 chỗ - MPV";
    private const string Hash = "$2a$11$3m3UqMzFHr5xfsLvPE47R.4tD/uIDb7SMZ/hTQFl1ZrGUl9zUdN.a";
    private const int DispatcherId = 2;

    public static void Seed(CarRentalDbContext db)
    {
        if (db.VehicleTypes.Any(t => t.TypeName == BusTypeName))
            return;

        var now = DateTime.UtcNow;
        SeedTypes(db);
        SeedPeople(db, now);
        SeedVehicles(db, now);
        db.SaveChanges();

        var vehicles = db.Vehicles.ToDictionary(v => v.LicensePlate);
        var users = db.Users.ToDictionary(u => u.Email);
        SeedOccupyingAndCatalog(db, now, vehicles, users);
        db.SaveChanges();

        SeedInspections(db, now);
        SeedMaintenance(db, now);
        SeedContracts(db, now);
        SeedPayments(db, now);
        SeedReviews(db, now);
        SeedIncidents(db, now);
        ApplyNewDriverTripStats(db);
        db.SaveChanges();
    }

    public static void ApplySchemaHelpers(CarRentalDbContext db)
    {
        db.Database.EnsureCreated();
        db.EnsureSqliteBookingRentalColumns();
        db.EnsureSqliteBookingPriceSnapshotColumns();
        db.EnsureSqliteVehicleTypePricingColumns();
        db.EnsureSqliteBookingModeSnapshotColumns();
        db.EnsureSqlitePaymentTypeColumn();
        db.EnsureSqliteVehicleInspectionsTable();
        db.EnsureSqliteBookingFinalAmountColumn();
        db.EnsureSqliteBookingFeesTable();
        db.EnsureSqliteUsersLockColumn();
        db.EnsureSqliteDriversActiveColumn();
        db.EnsureSqliteMaintenanceRecordsTable();
        db.EnsureSqliteBookingRecommendationColumn();
        db.EnsureSqliteContractsTable();
        db.EnsureSqliteInspectionConditionColumns();
        db.EnsureSqliteIncidentReportsTable();
        db.EnsureSqliteVehicleLegalColumns();
        db.EnsureSqliteLicensePlateUniqueIndex();
    }

    public static void CreateDemoDatabase(string path)
    {
        var options = new DbContextOptionsBuilder<CarRentalDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        using var db = new CarRentalDbContext(options);
        ApplySchemaHelpers(db);
        DbSeeder.Seed(db);
        Seed(db);
        db.FillVehicleTypePricingDefaults();
        db.ReconcileOpenAssignmentResourceStatus();
    }

    private static void SeedTypes(CarRentalDbContext db)
    {
        db.VehicleTypes.Add(Type(
            MpvTypeName, 7, 1_400_000, 16_000,
            "MPV 7 chỗ, phù hợp gia đình và đưa đón sân bay"));
        db.VehicleTypes.Add(Type(
            BusTypeName, 29, 8_000_000, 35_000,
            "Xe bus 29 chỗ, phù hợp đoàn doanh nghiệp / tour"));
        db.SaveChanges();
    }

    private static VehicleType Type(string name, int seats, decimal day, decimal km, string desc) => new()
    {
        TypeName = name,
        SeatCapacity = seats,
        PricePerDay = day,
        PricePerKm = km,
        DriverFeePerDay = PricingDefaults.DriverFeePerDay(day),
        SelfDrivePricePerDay = day,
        SelfDriveIncludedKmPerDay = PricingDefaults.IncludedKmPerDay,
        SelfDriveExtraKmPrice = km,
        WithDriverDepositAmount = PricingDefaults.WithDriverDeposit(day),
        SelfDriveDepositAmount = PricingDefaults.SelfDriveDeposit(day),
        Description = desc,
        IsActive = true
    };

    private static void SeedPeople(CarRentalDbContext db, DateTime now)
    {
        (string Email, string Name, string Phone, string Address, string Id, int Year)[] customers =
        [
            ("enterprise1@gmail.com", "Công ty TNHH Du Lịch Ánh Dương", "0908000001", "12 Nguyễn Huệ, Q1, TP.HCM", "031234567801", 1992),
            ("enterprise2@gmail.com", "Công ty CP Vận Tải Phương Nam", "0908000002", "45 Hai Bà Trưng, Q1, TP.HCM", "031234567802", 1988),
            ("enterprise3@gmail.com", "Tập đoàn Hòa Bình Tours", "0908000003", "88 Lê Lợi, Q1, TP.HCM", "031234567803", 1985),
            ("customer3@gmail.com", "Nguyễn Thị Hoa", "0912000003", "12 Trần Hưng Đạo, Q5", "079200000003", 1994),
            ("customer4@gmail.com", "Trần Văn Nam", "0912000004", "34 Lý Tự Trọng, Q1", "079200000004", 1991),
            ("customer5@gmail.com", "Lê Minh Châu", "0912000005", "56 Pasteur, Q3", "079200000005", 1996),
            ("customer6@gmail.com", "Phạm Quốc Bảo", "0912000006", "78 Điện Biên Phủ, Bình Thạnh", "079200000006", 1989),
            ("customer7@gmail.com", "Hoàng Thị Lan", "0912000007", "90 Nguyễn Thị Minh Khai, Q3", "079200000007", 1993),
            ("customer8@gmail.com", "Võ Minh Tuấn", "0912000008", "11 Cách Mạng Tháng 8, Q10", "079200000008", 1990),
            ("customer9@gmail.com", "Đặng Thanh Hà", "0912000009", "22 Hoàng Văn Thụ, Phú Nhuận", "079200000009", 1997),
            ("customer10@gmail.com", "Bùi Văn Khoa", "0912000010", "33 Phan Xích Long, Phú Nhuận", "079200000010", 1987),
            ("customer11@gmail.com", "Đỗ Thị Ngọc", "0912000011", "44 Nguyễn Trãi, Q5", "079200000011", 1995),
            ("customer12@gmail.com", "Ngô Gia Huy", "0912000012", "55 Võ Văn Tần, Q3", "079200000012", 1992),
            ("customer13@gmail.com", "Lý Thị Mai", "0912000013", "66 Nam Kỳ Khởi Nghĩa, Q3", "079200000013", 1998),
            ("customer14@gmail.com", "Phan Văn Đức", "0912000014", "77 Nguyễn Đình Chiểu, Q3", "079200000014", 1986),
            ("customer15@gmail.com", "Tô Thanh Tùng", "0912000015", "88 Lê Văn Sỹ, Tân Bình", "079200000015", 1994),
            ("customer16@gmail.com", "Huỳnh Thị Yến", "0912000016", "99 Cộng Hòa, Tân Bình", "079200000016", 1991),
            ("customer17@gmail.com", "Trịnh Minh Phúc", "0912000017", "100 Hoàng Hoa Thám, Tân Bình", "079200000017", 1993)
        ];

        foreach (var c in customers)
        {
            db.Users.Add(new User
            {
                Email = c.Email, PasswordHash = Hash, FullName = c.Name, Phone = c.Phone,
                RoleId = 3, CreatedAt = now
            });
        }

        (string Email, string Name, string Phone, string License, string Status)[] drivers =
        [
            ("driver4@carrental.vn", "Nguyễn Văn Phong", "0923000004", "B2-100000004", DriverStatuses.Available),
            ("driver5@carrental.vn", "Trần Minh Đức", "0923000005", "B2-100000005", DriverStatuses.Available),
            ("driver6@carrental.vn", "Lê Thị Hòa", "0923000006", "B2-100000006", DriverStatuses.Available),
            ("driver7@carrental.vn", "Phạm Quốc Đạt", "0923000007", "B2-100000007", DriverStatuses.Available),
            ("driver8@carrental.vn", "Hoàng Anh Tuấn", "0923000008", "B2-100000008", DriverStatuses.Available),
            ("driver9@carrental.vn", "Bùi Văn Long", "0923000009", "B2-100000009", DriverStatuses.Available),
            ("driver10@carrental.vn", "Đỗ Thị Lan", "0923000010", "B2-100000010", DriverStatuses.Available),
            ("driver11@carrental.vn", "Vũ Minh Tâm", "0923000011", "B2-100000011", DriverStatuses.Offline),
            ("driver12@carrental.vn", "Ngô Thanh Hải", "0923000012", "B2-100000012", DriverStatuses.Available)
        ];

        foreach (var d in drivers)
        {
            db.Users.Add(new User
            {
                Email = d.Email, PasswordHash = Hash, FullName = d.Name, Phone = d.Phone,
                RoleId = 4, CreatedAt = now
            });
        }

        db.SaveChanges();

        foreach (var c in customers)
        {
            var id = db.Users.Single(u => u.Email == c.Email).UserId;
            db.Customers.Add(new Customer
            {
                CustomerId = id,
                Address = c.Address,
                IdNumber = c.Id,
                DateOfBirth = new DateOnly(c.Year, 6, 15)
            });
        }

        var expiry = new DateOnly(2029, 6, 30);
        foreach (var d in drivers)
        {
            var id = db.Users.Single(u => u.Email == d.Email).UserId;
            db.Drivers.Add(new Driver
            {
                DriverId = id,
                LicenseNumber = d.License,
                LicenseExpiry = expiry,
                Status = d.Status,
                AverageRating = 0,
                TotalTrips = 0,
                IsActive = true
            });
        }

        db.SaveChanges();
    }

    private static void SeedVehicles(CarRentalDbContext db, DateTime now)
    {
        var near = DateOnly.FromDateTime(now.AddDays(14));
        var far = new DateOnly(2028, 11, 30);
        var mid = new DateOnly(2027, 8, 15);

        Vehicle Row(int typeId, string plate, string brand, string model, int year, string color,
            string status, int km, string? reg, DateOnly? regExp, DateOnly? insExp, DateOnly? insuExp) => new()
        {
            TypeId = typeId, LicensePlate = plate, Brand = brand, Model = model, Year = year, Color = color,
            Status = status, CurrentKm = km, CreatedAt = now,
            RegistrationNumber = reg,
            RegistrationExpiryDate = regExp,
            InspectionExpiryDate = insExp,
            InsuranceExpiryDate = insuExp
        };

        db.Vehicles.AddRange(
            Row(1, "51G-10007", "Toyota", "Camry", 2023, "Trắng", VehicleStatuses.Available, 32000, "VR-51G-10007", far, near, mid),
            Row(1, "51G-10008", "Honda", "City", 2022, "Xanh", VehicleStatuses.Available, 41000, "VR-51G-10008", far, mid, far),
            Row(1, "51G-10009", "Mazda", "3", 2019, "Đỏ", VehicleStatuses.Inactive, 180000, null, null, null, null),
            Row(1, "51G-10010", "Kia", "Cerato", 2021, "Bạc", VehicleStatuses.Available, 28500, "VR-51G-10010", far, mid, far),
            Row(2, "51H-20011", "Mazda", "CX-5", 2022, "Trắng", VehicleStatuses.Available, 62000, "VR-51H-20011", far, mid, far),
            Row(2, "51H-20012", "Hyundai", "SantaFe", 2020, "Đen", VehicleStatuses.Maintenance, 88000, "VR-51H-20012", far, mid, far),
            Row(2, "51H-20013", "Honda", "CR-V", 2023, "Xám", VehicleStatuses.Available, 47000, "VR-51H-20013", far, mid, far),
            Row(3, "51K-30014", "Ford", "Transit", 2021, "Trắng", VehicleStatuses.Available, 102000, "VR-51K-30014", far, mid, far),
            Row(3, "51K-30015", "Mercedes", "Sprinter", 2022, "Bạc", VehicleStatuses.Available, 71000, "VR-51K-30015", far, mid, far),
            Row(3, "51K-30016", "Hyundai", "County", 2018, "Trắng", VehicleStatuses.Inactive, 155000, "VR-51K-30016", far, mid, far),
            Row(4, "51L-40017", "Mercedes", "Exclusive", 2023, "Đen", VehicleStatuses.Available, 18000, "VR-51L-40017", far, mid, far),
            Row(4, "51L-40018", "Hyundai", "Solati Limo", 2024, "Đen", VehicleStatuses.Inactive, 9000, null, null, null, null),
            Row(5, "51M-50019", "Toyota", "Innova", 2022, "Bạc", VehicleStatuses.Available, 54000, "VR-51M-50019", far, mid, far),
            Row(5, "51M-50020", "Kia", "Carnival", 2021, "Trắng", VehicleStatuses.Maintenance, 36000, "VR-51M-50020", far, mid, far),
            Row(5, "51M-50021", "Mitsubishi", "Xpander", 2023, "Cam", VehicleStatuses.Available, 29000, "VR-51M-50021", far, mid, far),
            Row(6, "51N-60022", "Thaco", "Town", 2022, "Trắng", VehicleStatuses.Available, 67000, "VR-51N-60022", far, mid, far),
            Row(6, "51N-60023", "Samco", "Universe", 2021, "Xanh", VehicleStatuses.Available, 91000, "VR-51N-60023", far, mid, far),
            Row(6, "51N-60024", "Thaco", "Meadow", 2023, "Trắng", VehicleStatuses.Available, 44000, "VR-51N-60024", far, mid, far));

        var v1 = db.Vehicles.Single(x => x.LicensePlate == "51A-12345");
        var v3 = db.Vehicles.Single(x => x.LicensePlate == "51C-11111");
        var v4 = db.Vehicles.Single(x => x.LicensePlate == "51D-22222");
        var v5 = db.Vehicles.Single(x => x.LicensePlate == "51E-33333");
        var v6 = db.Vehicles.Single(x => x.LicensePlate == "51F-44444");
        v1.RegistrationNumber = "VR-51A-12345";
        v1.RegistrationExpiryDate = far;
        v1.InspectionExpiryDate = mid;
        v1.InsuranceExpiryDate = far;
        v3.RegistrationNumber = "VR-51C-11111";
        v3.RegistrationExpiryDate = far;
        v3.InspectionExpiryDate = mid;
        v3.InsuranceExpiryDate = far;
        v4.RegistrationNumber = "VR-51D-22222";
        v4.RegistrationExpiryDate = far;
        v4.InspectionExpiryDate = mid;
        v4.InsuranceExpiryDate = far;
        v5.RegistrationNumber = "VR-51E-33333";
        v5.RegistrationExpiryDate = far;
        v5.InspectionExpiryDate = mid;
        v5.InsuranceExpiryDate = far;
        v6.RegistrationNumber = "VR-51F-44444";
        v6.RegistrationExpiryDate = far;
        v6.InspectionExpiryDate = mid;
        v6.InsuranceExpiryDate = far;
    }

    private static void SeedOccupyingAndCatalog(
        CarRentalDbContext db, DateTime now,
        Dictionary<string, Vehicle> vehicles,
        Dictionary<string, User> users)
    {
        int C(string email) => users[email].UserId;
        int V(string plate) => vehicles[plate].VehicleId;
        int D(string email) => users[email].UserId;
        DateTime T(int days, int hour) =>
            new DateTime(now.Year, now.Month, now.Day, hour, 0, 0, DateTimeKind.Utc).AddDays(days);

        // 3 InProgress + 6 Assigned (plus Core booking #2 on driver1). One driver = one open.
        AddAssigned(db, now, C("customer3@gmail.com"), 1, RentalModes.WithDriver, BookingStatuses.InProgress,
            T(-1, 8), T(1, 18), V("51A-12345"), D("driver2@carrental.vn"), TripAssignmentStatuses.InProgress,
            T(-1, 7), T(-1, 8), null, false, 2_400_000, 800_000, "Sedan đang chạy nội thành");

        AddAssigned(db, now, C("customer4@gmail.com"), 1, RentalModes.WithDriver, BookingStatuses.Assigned,
            T(8, 8), T(9, 18), V("51G-10010"), D("driver4@carrental.vn"), TripAssignmentStatuses.Assigned,
            T(-2, 10), null, null, true, 1_600_000, 800_000, "Sedan lịch tuần sau");

        AddAssigned(db, now, C("customer5@gmail.com"), 1, RentalModes.WithDriver, BookingStatuses.Assigned,
            T(15, 8), T(16, 18), V("51A-12345"), D("driver5@carrental.vn"), TripAssignmentStatuses.Accepted,
            T(-1, 11), null, null, false, 1_760_000, 800_000, "Sedan lịch đã nhận chuyến");

        AddAssigned(db, now, C("enterprise1@gmail.com"), 3, RentalModes.WithDriver, BookingStatuses.InProgress,
            T(-2, 7), T(1, 19), V("51K-30015"), D("driver7@carrental.vn"), TripAssignmentStatuses.InProgress,
            T(-2, 6), T(-2, 7), null, true, 5_200_000, 2_500_000, "Van đoàn Ánh Dương đang chạy");

        AddAssigned(db, now, C("enterprise2@gmail.com"), 3, RentalModes.WithDriver, BookingStatuses.Assigned,
            T(5, 8), T(6, 18), V("51K-30015"), D("driver6@carrental.vn"), TripAssignmentStatuses.Assigned,
            T(-3, 9), null, null, false, 4_800_000, 2_500_000, "Van Phương Nam tuần tới");

        AddAssigned(db, now, C("enterprise3@gmail.com"), 3, RentalModes.WithDriver, BookingStatuses.Assigned,
            T(11, 8), T(12, 18), V("51K-30015"), D("driver8@carrental.vn"), TripAssignmentStatuses.Accepted,
            T(-2, 12), null, null, false, 4_900_000, 2_500_000, "Van Hòa Bình Tours");

        AddAssigned(db, now, C("enterprise1@gmail.com"), 6, RentalModes.WithDriver, BookingStatuses.InProgress,
            T(-1, 6), T(2, 20), V("51N-60023"), D("driver9@carrental.vn"), TripAssignmentStatuses.InProgress,
            T(-1, 5), T(-1, 6), null, true, 18_000_000, 8_000_000, "Bus đoàn Ánh Dương — đang phục vụ");

        AddAssigned(db, now, C("enterprise2@gmail.com"), 6, RentalModes.WithDriver, BookingStatuses.Assigned,
            T(7, 7), T(8, 21), V("51N-60023"), D("driver12@carrental.vn"), TripAssignmentStatuses.Accepted,
            T(-2, 14), null, null, true, 16_500_000, 8_000_000, "Bus Phương Nam đã nhận");

        AddAssigned(db, now, C("enterprise3@gmail.com"), 6, RentalModes.WithDriver, BookingStatuses.Assigned,
            T(13, 7), T(14, 21), V("51N-60023"), D("driver10@carrental.vn"), TripAssignmentStatuses.Assigned,
            T(-1, 15), null, null, false, 16_800_000, 8_000_000, "Bus Hòa Bình Tours");

        var doneStart = T(-31, 8);
        var doneEnd = T(-31, 18);
        AddAssigned(db, now, C("customer6@gmail.com"), 3, RentalModes.WithDriver, BookingStatuses.Completed,
            doneStart, doneEnd, V("51K-30014"), D("driver7@carrental.vn"), TripAssignmentStatuses.Completed,
            doneStart.AddHours(-2), doneStart, doneEnd.AddMinutes(30), false, 4_700_000, 2_500_000, "Van khách lẻ",
            createdOffsetDays: -31);

        // Confirmed (no assignment) × 6
        AddPlain(db, now, C("customer7@gmail.com"), 1, RentalModes.WithDriver, BookingStatuses.Confirmed,
            T(21, 8), T(22, 18), false, 1_600_000, 800_000, "Sedan chờ điều phối");
        AddPlain(db, now, C("customer8@gmail.com"), 2, RentalModes.WithDriver, BookingStatuses.Confirmed,
            T(22, 8), T(24, 18), true, 4_200_000, 1_200_000, "SUV chờ điều phối");
        AddPlain(db, now, C("customer9@gmail.com"), 5, RentalModes.WithDriver, BookingStatuses.Confirmed,
            T(23, 8), T(25, 18), false, 4_800_000, 1_400_000, "MPV chờ điều phối");
        AddPlain(db, now, C("enterprise1@gmail.com"), 6, RentalModes.WithDriver, BookingStatuses.Confirmed,
            T(25, 7), T(26, 20), true, 16_000_000, 8_000_000, "Bus Ánh Dương chờ gán xe");
        AddPlain(db, now, C("customer10@gmail.com"), 1, RentalModes.SelfDrive, BookingStatuses.Confirmed,
            T(21, 9), T(22, 17), false, 1_200_000, 4_000_000, "Sedan tự lái đã xác nhận");
        AddPlain(db, now, C("customer11@gmail.com"), 2, RentalModes.SelfDrive, BookingStatuses.Confirmed,
            T(26, 8), T(27, 18), false, 2_400_000, 6_000_000, "SUV tự lái đã xác nhận");

        // Pending × 7 new (Core #1 already Pending) → 8
        AddPlain(db, now, C("enterprise3@gmail.com"), 6, RentalModes.WithDriver, BookingStatuses.Pending,
            T(28, 7), T(29, 20), true, 16_200_000, 8_000_000, "Bus Hòa Bình — đơn mới");
        AddPlain(db, now, C("customer12@gmail.com"), 1, RentalModes.WithDriver, BookingStatuses.Pending,
            T(27, 8), T(28, 18), false, 1_500_000, 800_000, "Sedan pending");
        AddPlain(db, now, C("customer13@gmail.com"), 2, RentalModes.WithDriver, BookingStatuses.Pending,
            T(27, 9), T(28, 19), false, 2_800_000, 1_200_000, "SUV pending");
        AddPlain(db, now, C("customer14@gmail.com"), 4, RentalModes.WithDriver, BookingStatuses.Pending,
            T(30, 8), T(31, 18), false, 6_000_000, 3_500_000, "Limousine pending");
        AddPlain(db, now, C("customer15@gmail.com"), 1, RentalModes.SelfDrive, BookingStatuses.Pending,
            T(24, 8), T(25, 18), true, 1_100_000, 4_000_000, "Sedan tự lái pending");
        AddPlain(db, now, C("customer16@gmail.com"), 5, RentalModes.SelfDrive, BookingStatuses.Pending,
            T(29, 8), T(30, 18), false, 2_200_000, 7_000_000, "MPV tự lái pending");
        AddPlain(db, now, C("customer17@gmail.com"), 2, RentalModes.SelfDrive, BookingStatuses.Pending,
            T(32, 8), T(33, 18), false, 2_000_000, 6_000_000, "SUV tự lái pending");

        // Completed WithDriver × 14 (staggered past — do not occupy)
        var wdDone = new (string Customer, int Type, string Plate, string Driver, int Day, byte Stars, bool Rec, decimal Amt)[]
        {
            ("customer3@gmail.com", 1, "51B-67890", "driver2@carrental.vn", -24, 5, true, 1_800_000),
            ("customer4@gmail.com", 1, "51G-10007", "driver4@carrental.vn", -22, 5, true, 1_700_000),
            ("customer5@gmail.com", 1, "51G-10008", "driver5@carrental.vn", -20, 4, true, 1_650_000),
            ("customer6@gmail.com", 2, "51C-11111", "driver6@carrental.vn", -18, 4, true, 3_200_000),
            ("customer7@gmail.com", 2, "51D-22222", "driver8@carrental.vn", -16, 3, false, 3_100_000),
            ("customer8@gmail.com", 2, "51H-20011", "driver10@carrental.vn", -14, 2, false, 3_000_000),
            ("enterprise1@gmail.com", 3, "51E-33333", "driver12@carrental.vn", -12, 2, true, 5_500_000),
            ("enterprise2@gmail.com", 3, "51K-30014", "driver2@carrental.vn", -10, 3, false, 5_400_000),
            ("customer9@gmail.com", 4, "51L-40017", "driver4@carrental.vn", -9, 5, true, 7_200_000),
            ("customer10@gmail.com", 5, "51M-50019", "driver5@carrental.vn", -8, 4, false, 3_600_000),
            ("customer11@gmail.com", 5, "51M-50021", "driver6@carrental.vn", -7, 3, false, 3_400_000),
            ("enterprise1@gmail.com", 6, "51N-60022", "driver8@carrental.vn", -6, 5, true, 17_500_000),
            ("enterprise2@gmail.com", 6, "51N-60024", "driver10@carrental.vn", -5, 4, true, 16_900_000),
            ("customer12@gmail.com", 1, "51G-10007", "driver12@carrental.vn", -4, 1, false, 1_550_000),
            ("customer13@gmail.com", 1, "51B-67890", "driver4@carrental.vn", -26, 5, false, 1_600_000),
            ("customer14@gmail.com", 2, "51H-20013", "driver5@carrental.vn", -25, 4, false, 3_050_000),
            ("customer15@gmail.com", 5, "51M-50019", "driver6@carrental.vn", -3, 2, false, 3_300_000),
            ("customer16@gmail.com", 3, "51K-30014", "driver8@carrental.vn", -27, 1, false, 5_200_000),
            ("customer17@gmail.com", 1, "51G-10008", "driver10@carrental.vn", -28, 5, true, 1_720_000),
            ("customer3@gmail.com", 2, "51D-22222", "driver12@carrental.vn", -29, 4, false, 3_080_000)
        };
        foreach (var x in wdDone)
        {
            var start = T(x.Day, 8);
            var end = T(x.Day, 18);
            AddAssigned(db, now, C(x.Customer), x.Type, RentalModes.WithDriver, BookingStatuses.Completed,
                start, end, V(x.Plate), D(x.Driver), TripAssignmentStatuses.Completed,
                start.AddHours(-2), start, end.AddMinutes(30), x.Rec, x.Amt,
                DepositFor(x.Type), "Chuyến hoàn thành", createdOffsetDays: x.Day);
        }

        // Completed SelfDrive × 10
        var sdDone = new (string Customer, int Type, string Plate, int Day, bool Rec, decimal Amt)[]
        {
            ("customer13@gmail.com", 1, "51B-67890", -23, true, 1_200_000),
            ("customer14@gmail.com", 1, "51G-10008", -21, false, 1_150_000),
            ("customer15@gmail.com", 2, "51H-20013", -19, false, 2_500_000),
            ("customer16@gmail.com", 5, "51M-50021", -15, false, 2_800_000),
            ("customer17@gmail.com", 1, "51G-10007", -13, false, 1_100_000),
            ("customer3@gmail.com", 2, "51H-20011", -11, true, 2_300_000),
            ("customer4@gmail.com", 1, "51B-67890", -8, false, 1_050_000),
            ("customer5@gmail.com", 5, "51M-50019", -2, false, 2_700_000)
        };
        foreach (var x in sdDone)
        {
            var start = T(x.Day, 9);
            var end = T(x.Day, 17);
            AddSelfDriveCompleted(db, now, C(x.Customer), x.Type, start, end, V(x.Plate), x.Rec, x.Amt,
                DepositFor(x.Type) * 5, x.Day);
        }

        // Cancelled × 10 (5 WD with cancelled assignment + 5 SD/plain)
        for (var i = 0; i < 5; i++)
        {
            var start = T(34 + i, 8);
            var end = T(35 + i, 18);
            var customer = C($"customer{3 + i}@gmail.com");
            AddAssigned(db, now, customer, 1, RentalModes.WithDriver, BookingStatuses.Cancelled,
                start, end, V("51G-10007"), D("driver4@carrental.vn"), TripAssignmentStatuses.Cancelled,
                now.AddDays(-2 + i), null, null, i == 0, 1_400_000, 800_000, "Đơn hủy sau khi gán",
                createdOffsetDays: -2 + i);
        }

        AddPlain(db, now, C("customer8@gmail.com"), 2, RentalModes.SelfDrive, BookingStatuses.Cancelled,
            T(36, 8), T(37, 18), false, 2_000_000, 6_000_000, "Hủy tự lái", createdOffsetDays: -1);
        AddPlain(db, now, C("customer9@gmail.com"), 1, RentalModes.SelfDrive, BookingStatuses.Cancelled,
            T(37, 8), T(38, 18), true, 1_000_000, 4_000_000, "Hủy tự lái 2", createdOffsetDays: -2);
        AddPlain(db, now, C("customer10@gmail.com"), 5, RentalModes.WithDriver, BookingStatuses.Cancelled,
            T(38, 8), T(39, 18), false, 3_000_000, 1_400_000, "Hủy MPV", createdOffsetDays: -3);
        AddPlain(db, now, C("customer11@gmail.com"), 4, RentalModes.WithDriver, BookingStatuses.Cancelled,
            T(39, 8), T(40, 18), false, 5_000_000, 3_500_000, "Hủy Limousine", createdOffsetDays: -4);
        AddPlain(db, now, C("customer12@gmail.com"), 2, RentalModes.SelfDrive, BookingStatuses.Cancelled,
            T(40, 8), T(41, 18), false, 2_100_000, 6_000_000, "Hủy SUV tự lái", createdOffsetDays: -5);
    }

    private static decimal DepositFor(int typeId) => typeId switch
    {
        1 => 800_000,
        2 => 1_200_000,
        3 => 2_500_000,
        4 => 3_500_000,
        5 => 1_400_000,
        6 => 8_000_000,
        _ => 800_000
    };

    private static Booking AddPlain(
        CarRentalDbContext db, DateTime now, int customerId, int typeId, string mode, string status,
        DateTime start, DateTime end, bool recommended, decimal amount, decimal? deposit, string notes,
        int createdOffsetDays = 0)
    {
        var created = now.AddDays(createdOffsetDays);
        var booking = NewBooking(customerId, typeId, mode, status, start, end, recommended, amount, deposit, notes, created);
        db.Bookings.Add(booking);
        db.SaveChanges();
        AddHistory(db, booking, now);
        return booking;
    }

    private static Booking AddAssigned(
        CarRentalDbContext db, DateTime now, int customerId, int typeId, string mode, string status,
        DateTime start, DateTime end, int vehicleId, int driverId, string assignStatus,
        DateTime assignedAt, DateTime? startedAt, DateTime? completedAt, bool recommended,
        decimal amount, decimal? deposit, string notes, int createdOffsetDays = 0)
    {
        var created = now.AddDays(Math.Min(createdOffsetDays, 0));
        var booking = NewBooking(customerId, typeId, mode, status, start, end, recommended, amount, deposit, notes, created);
        if (status is BookingStatuses.Assigned or BookingStatuses.InProgress or BookingStatuses.Completed)
            booking.AssignedVehicleId = vehicleId;
        if (status == BookingStatuses.Completed)
            booking.UpdatedAt = completedAt ?? end;
        db.Bookings.Add(booking);
        db.SaveChanges();

        db.TripAssignments.Add(new TripAssignment
        {
            BookingId = booking.BookingId,
            DriverId = driverId,
            VehicleId = vehicleId,
            AssignedBy = DispatcherId,
            AssignedAt = assignedAt,
            Status = assignStatus,
            StartedAt = startedAt,
            CompletedAt = completedAt
        });
        db.SaveChanges();
        AddHistory(db, booking, now);
        return booking;
    }

    private static void AddSelfDriveCompleted(
        CarRentalDbContext db, DateTime now, int customerId, int typeId,
        DateTime start, DateTime end, int vehicleId, bool recommended, decimal amount, decimal deposit, int day)
    {
        var booking = NewBooking(customerId, typeId, RentalModes.SelfDrive, BookingStatuses.Completed,
            start, end, recommended, amount, deposit, "Tự lái hoàn thành", now.AddDays(day));
        booking.AssignedVehicleId = vehicleId;
        booking.UpdatedAt = end;
        db.Bookings.Add(booking);
        db.SaveChanges();
        AddHistory(db, booking, now);
    }

    private static Booking NewBooking(
        int customerId, int typeId, string mode, string status,
        DateTime start, DateTime end, bool recommended, decimal amount, decimal? deposit,
        string notes, DateTime created) => new()
    {
        CustomerId = customerId,
        VehicleTypeId = typeId,
        PickupAddress = "Sân bay Tân Sơn Nhất, TP.HCM",
        DropoffAddress = typeId == 6 ? "Khu công nghiệp VSIP, Bình Dương" : "Trung tâm TP.HCM",
        StartDate = start,
        EndDate = end,
        EstimatedDistance = typeId == 6 ? 180 : 80,
        TotalAmount = amount,
        QuotedDepositAmount = deposit,
        Status = status,
        RentalMode = mode,
        SourceRecommended = recommended,
        Notes = notes,
        CreatedAt = created
    };

    private static void AddHistory(CarRentalDbContext db, Booking booking, DateTime now)
    {
        var t0 = booking.CreatedAt == default ? now.AddDays(-1) : booking.CreatedAt;
        void Step(string? old, string neu, string note, int hours)
        {
            db.BookingStatusHistories.Add(new BookingStatusHistory
            {
                BookingId = booking.BookingId,
                OldStatus = old,
                NewStatus = neu,
                ChangedBy = old is null ? booking.CustomerId : DispatcherId,
                Note = note,
                ChangedAt = t0.AddHours(hours)
            });
        }

        Step(null, BookingStatuses.Pending, "Khách tạo đơn đặt xe", 0);
        switch (booking.Status)
        {
            case BookingStatuses.Pending:
                break;
            case BookingStatuses.Confirmed:
                Step(BookingStatuses.Pending, BookingStatuses.Confirmed, "Điều phối xác nhận đơn", 2);
                break;
            case BookingStatuses.Assigned:
                Step(BookingStatuses.Pending, BookingStatuses.Confirmed, "Điều phối xác nhận đơn", 2);
                Step(BookingStatuses.Confirmed, BookingStatuses.Assigned, "Phân công tài xế / xe", 4);
                break;
            case BookingStatuses.InProgress:
                Step(BookingStatuses.Pending, BookingStatuses.Confirmed, "Điều phối xác nhận đơn", 2);
                Step(BookingStatuses.Confirmed, BookingStatuses.Assigned, "Phân công tài xế / xe", 4);
                Step(BookingStatuses.Assigned, BookingStatuses.InProgress, "Bắt đầu chuyến", 6);
                break;
            case BookingStatuses.Completed when booking.RentalMode == RentalModes.SelfDrive:
                Step(BookingStatuses.Pending, BookingStatuses.Confirmed, "Điều phối xác nhận đơn", 2);
                Step(BookingStatuses.Confirmed, BookingStatuses.Assigned, "Gán xe tự lái", 4);
                Step(BookingStatuses.Assigned, BookingStatuses.Completed, "Hoàn thành tự lái", 12);
                break;
            case BookingStatuses.Completed:
                Step(BookingStatuses.Pending, BookingStatuses.Confirmed, "Điều phối xác nhận đơn", 2);
                Step(BookingStatuses.Confirmed, BookingStatuses.Assigned, "Phân công tài xế / xe", 4);
                Step(BookingStatuses.Assigned, BookingStatuses.Completed, "Hoàn thành chuyến", 12);
                break;
            case BookingStatuses.Cancelled:
                Step(BookingStatuses.Pending, BookingStatuses.Cancelled, "Hủy đơn", 3);
                break;
        }
    }

    private static void SeedInspections(CarRentalDbContext db, DateTime now)
    {
        var completed = db.Bookings
            .Where(b => b.Status == BookingStatuses.Completed && b.AssignedVehicleId != null)
            .OrderBy(b => b.BookingId)
            .Take(14)
            .ToList();

        foreach (var booking in completed)
        {
            var vehicle = db.Vehicles.Find(booking.AssignedVehicleId!.Value)!;
            var startKm = Math.Max(0, vehicle.CurrentKm - 80);
            db.VehicleInspections.Add(new VehicleInspection
            {
                BookingId = booking.BookingId,
                VehicleId = vehicle.VehicleId,
                InspectionType = VehicleInspectionTypes.Handover,
                ActualAt = booking.StartDate,
                OdometerKm = startKm,
                FuelLevel = 70,
                Condition = "Tốt",
                ExteriorCondition = "Sơn zin, không móp",
                TechnicalCondition = "Động cơ ổn",
                Notes = "Giao xe",
                CreatedAt = booking.StartDate
            });
            db.VehicleInspections.Add(new VehicleInspection
            {
                BookingId = booking.BookingId,
                VehicleId = vehicle.VehicleId,
                InspectionType = VehicleInspectionTypes.Return,
                ActualAt = booking.EndDate,
                OdometerKm = vehicle.CurrentKm,
                FuelLevel = 40,
                Condition = "Tốt",
                ExteriorCondition = "Bình thường",
                TechnicalCondition = "Đèn, phanh OK",
                Notes = "Trả xe",
                CreatedAt = booking.EndDate
            });
        }
    }

    private static void SeedMaintenance(CarRentalDbContext db, DateTime now)
    {
        db.MaintenanceRecords.RemoveRange(db.MaintenanceRecords);
        db.SaveChanges();

        var byPlate = db.Vehicles.ToDictionary(v => v.LicensePlate);

        void Completed(string plate, DateTime when, int? odoOffset, string type, string notes)
        {
            var v = byPlate[plate];
            var odo = v.CurrentKm + (odoOffset ?? 0);
            db.MaintenanceRecords.Add(new MaintenanceRecord
            {
                VehicleId = v.VehicleId,
                MaintenanceType = type,
                ScheduledDate = when,
                CompletedDate = when,
                OdometerAtMaintenance = odo,
                Cost = 1_500_000,
                Notes = notes,
                CreatedAt = when
            });
        }

        void OpenSchedule(string plate, DateTime when, string notes)
        {
            var v = byPlate[plate];
            db.MaintenanceRecords.Add(new MaintenanceRecord
            {
                VehicleId = v.VehicleId,
                MaintenanceType = MaintenanceTypes.Scheduled,
                ScheduledDate = when,
                CompletedDate = null,
                OdometerAtMaintenance = v.CurrentKm,
                Notes = notes,
                CreatedAt = now
            });
        }

        // 1. Vừa bảo trì (~7 ngày, odo ≈ CurrentKm)
        foreach (var plate in new[]
                 { "51B-67890", "51C-11111", "51E-33333", "51G-10007", "51H-20011", "51K-30014",
                     "51L-40017", "51M-50019", "51M-50021", "51N-60022", "51N-60024", "51A-12345",
                     "51G-10010", "51K-30015", "51N-60023" })
            Completed(plate, now.AddDays(-7), 0, MaintenanceTypes.Scheduled, "Bảo trì định kỳ gần đây");

        // 2. Gần 5000 km
        Completed("51G-10008", now.AddDays(-12), -4800, MaintenanceTypes.Scheduled, "Gần ngưỡng 5000 km");

        // 3. Vượt 5000 km + nhiều record
        Completed("51H-20012", now.AddDays(-400), -18000, MaintenanceTypes.Repair, "Sửa chữa cũ");
        Completed("51H-20012", now.AddDays(-15), -6200, MaintenanceTypes.Scheduled, "Quá hạn kilomet");

        // 4. Gần 180 ngày
        Completed("51H-20013", now.AddDays(-170), -200, MaintenanceTypes.Scheduled, "Gần 180 ngày");
        Completed("51D-22222", now.AddDays(-170), -150, MaintenanceTypes.Scheduled, "Gần 180 ngày");

        // 5. Vượt 180 ngày + nhiều record
        Completed("51M-50020", now.AddDays(-400), -8000, MaintenanceTypes.Repair, "Sửa chữa cũ");
        Completed("51M-50020", now.AddDays(-200), -80, MaintenanceTypes.Scheduled, "Quá hạn thời gian");
        Completed("51F-44444", now.AddDays(-20), -5500, MaintenanceTypes.Scheduled, "Xe đang Maintenance — quá hạn km");

        // 6. Không completed: 51K-30016, 51L-40018, 51G-10009 — skip
        OpenSchedule("51G-10009", now.AddDays(10), "Inactive — lịch mở, chưa hoàn thành");

        // 8. Scheduled chưa hoàn thành trên xe vừa bảo trì
        OpenSchedule("51H-20011", now.AddDays(14), "Lịch định kỳ tiếp theo");
        OpenSchedule("51N-60022", now.AddDays(20), "Lịch bus sắp tới");

        Completed("51G-10007", now.AddDays(-90), -3000, MaintenanceTypes.Inspection, "Đăng kiểm cũ");
        Completed("51A-12345", now.AddDays(-60), -1200, MaintenanceTypes.Inspection, "Kiểm tra định kỳ cũ");
        Completed("51N-60024", now.AddDays(-45), -800, MaintenanceTypes.Inspection, "Kiểm tra bus");
        Completed("51M-50021", now.AddDays(-40), -500, MaintenanceTypes.Repair, "Thay má phanh");
        Completed("51L-40017", now.AddDays(-55), -400, MaintenanceTypes.Inspection, "Kiểm tra limo");
    }

    private static void SeedContracts(CarRentalDbContext db, DateTime now)
    {
        var types = db.VehicleTypes.ToDictionary(t => t.TypeId, t => t.TypeName);
        var names = db.Users.ToDictionary(u => u.UserId, u => u.FullName);

        var signed = db.Bookings
            .Where(b => b.Status == BookingStatuses.Completed || b.Status == BookingStatuses.InProgress)
            .OrderBy(b => b.BookingId)
            .Take(14)
            .ToList();
        var issued = db.Bookings
            .Where(b => b.Status == BookingStatuses.Assigned || b.Status == BookingStatuses.Confirmed)
            .OrderBy(b => b.BookingId)
            .Take(6)
            .ToList();
        var voided = db.Bookings
            .Where(b => b.Status == BookingStatuses.Cancelled)
            .OrderBy(b => b.BookingId)
            .Take(2)
            .ToList();

        void Add(Booking b, string status)
        {
            db.Contracts.Add(new Contract
            {
                BookingId = b.BookingId,
                ContractNumber = $"CTR-D-{b.BookingId:D6}",
                Status = status,
                CustomerId = b.CustomerId,
                CustomerName = names.GetValueOrDefault(b.CustomerId, "Khách"),
                VehicleTypeName = types.GetValueOrDefault(b.VehicleTypeId, "Xe"),
                RentalMode = b.RentalMode,
                PickupAddress = b.PickupAddress,
                DropoffAddress = b.DropoffAddress,
                StartDate = b.StartDate,
                EndDate = b.EndDate,
                TotalAmount = b.TotalAmount,
                DepositAmount = b.QuotedDepositAmount,
                CreatedAt = b.CreatedAt.AddHours(1),
                SignedAt = status == ContractStatuses.Signed ? b.CreatedAt.AddHours(3) : null
            });
        }

        foreach (var b in signed) Add(b, ContractStatuses.Signed);
        foreach (var b in issued) Add(b, ContractStatuses.Issued);
        foreach (var b in voided) Add(b, ContractStatuses.Voided);
    }

    private static void SeedPayments(CarRentalDbContext db, DateTime now)
    {
        var paid = db.Bookings
            .Where(b => b.BookingId != 2 && b.QuotedDepositAmount != null
                        && (b.Status == BookingStatuses.Completed
                            || b.Status == BookingStatuses.Assigned
                            || b.Status == BookingStatuses.InProgress))
            .OrderBy(b => b.BookingId)
            .Take(25)
            .ToList();
        var pending = db.Bookings
            .Where(b => b.QuotedDepositAmount != null
                        && (b.Status == BookingStatuses.Pending || b.Status == BookingStatuses.Confirmed)
                        && b.BookingId != 1)
            .OrderBy(b => b.BookingId)
            .Take(8)
            .ToList();
        var failed = db.Bookings
            .Where(b => b.Status == BookingStatuses.Cancelled && b.QuotedDepositAmount != null)
            .OrderBy(b => b.BookingId)
            .Take(6)
            .ToList();

        var n = 2;
        foreach (var b in paid)
        {
            db.Payments.Add(Pay(b, PaymentStatuses.Paid, now.AddDays(-Math.Abs(n % 20)), now.AddDays(-Math.Abs(n % 20) + 0.1), n));
            n++;
        }
        foreach (var b in pending)
        {
            db.Payments.Add(Pay(b, PaymentStatuses.Pending, now.AddDays(-Math.Abs(n % 10)), null, n));
            n++;
        }
        foreach (var b in failed)
        {
            db.Payments.Add(Pay(b, PaymentStatuses.Failed, now.AddDays(-Math.Abs(n % 12)), null, n));
            n++;
        }
    }

    private static Payment Pay(Booking b, string status, DateTime created, DateTime? paidAt, int n) => new()
    {
        BookingId = b.BookingId,
        PaymentType = PaymentTypes.Deposit,
        Amount = b.QuotedDepositAmount ?? 800_000,
        Method = n % 3 == 0 ? PaymentMethods.MoMo : n % 3 == 1 ? PaymentMethods.Cash : PaymentMethods.BankTransfer,
        Status = status,
        TransactionRef = $"TXN-DEMO-{n:D4}",
        PaidAt = status == PaymentStatuses.Paid ? paidAt : null,
        CreatedAt = created
    };

    private static void SeedReviews(CarRentalDbContext db, DateTime now)
    {
        var rows = db.Bookings
            .Where(b => b.Status == BookingStatuses.Completed && b.RentalMode == RentalModes.WithDriver)
            .Join(db.TripAssignments, b => b.BookingId, t => t.BookingId, (b, t) => new { b, t })
            .OrderBy(x => x.b.BookingId)
            .ToList();

        byte[] ratings = [5, 5, 4, 4, 3, 2, 2, 3, 5, 4, 3, 1];
        string?[] comments =
        [
            "Tài xế đúng giờ, xe sạch.",
            "Rất hài lòng, sẽ thuê lại.",
            "Ổn, chạy êm.",
            null,
            "Xe hơi ồn, tài xế chạy nhanh.",
            "Đến muộn 20 phút, cần cải thiện.",
            "Xe bẩn, điều hòa yếu.",
            "Thái độ tài xế chưa tốt.",
            "Bus êm, phù hợp đoàn công ty.",
            "Tốt, đúng lộ trình.",
            "Cần thông báo trễ giờ sớm hơn.",
            "Tài xế thiếu thân thiện, chuyến không đúng giờ."
        ];

        for (var i = 0; i < ratings.Length && i < rows.Count; i++)
        {
            var row = rows[i];
            db.Reviews.Add(new Review
            {
                BookingId = row.b.BookingId,
                CustomerId = row.b.CustomerId,
                DriverId = row.t.DriverId,
                Rating = ratings[i],
                Comment = comments[i],
                CreatedAt = (row.b.UpdatedAt ?? now).AddHours(2)
            });
        }
    }

    private static void SeedIncidents(CarRentalDbContext db, DateTime now)
    {
        var assignments = db.TripAssignments
            .Where(t => t.Status != TripAssignmentStatuses.Cancelled)
            .OrderBy(t => t.AssignmentId)
            .Take(8)
            .ToList();
        string[] types =
        [
            IncidentTypes.Accident, IncidentTypes.Accident,
            IncidentTypes.VehicleIssue, IncidentTypes.VehicleIssue,
            IncidentTypes.CustomerIssue, IncidentTypes.CustomerIssue,
            IncidentTypes.Other, IncidentTypes.Other
        ];
        string[] desc =
        [
            "Trầy gương chiếu hậu khi đỗ.",
            "Va chạm nhẹ cản trước.",
            "Đèn báo động cơ sáng giữa đường.",
            "Lốp non hơi, đã thay dự phòng.",
            "Khách yêu cầu đổi lộ trình đột xuất.",
            "Khách phàn nàn tài xế đến trễ.",
            "Kẹt xe kéo dài do sự cố giao thông.",
            "Khách để quên đồ trên xe."
        ];
        for (var i = 0; i < assignments.Count; i++)
        {
            var a = assignments[i];
            db.IncidentReports.Add(new IncidentReport
            {
                BookingId = a.BookingId,
                AssignmentId = a.AssignmentId,
                DriverId = a.DriverId,
                IncidentType = types[i],
                Description = desc[i],
                OccurredAt = a.StartedAt ?? a.AssignedAt,
                Status = IncidentStatuses.Open,
                CreatedAt = now.AddDays(-8 + i)
            });
        }
    }

    private static void ApplyNewDriverTripStats(CarRentalDbContext db)
    {
        var newDrivers = db.Drivers.Where(d => d.DriverId >= 8).ToList();
        foreach (var driver in newDrivers)
        {
            var completed = db.TripAssignments.Count(t =>
                t.DriverId == driver.DriverId && t.Status == TripAssignmentStatuses.Completed);
            driver.TotalTrips = completed;
            var ratings = db.Reviews.Where(r => r.DriverId == driver.DriverId).Select(r => (decimal)r.Rating).ToList();
            if (ratings.Count > 0)
                driver.AverageRating = Math.Round(ratings.Average(), 2);
        }
    }
}
