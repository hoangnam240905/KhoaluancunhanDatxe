# Car Rental System

He thong cho thue xe du lich truc tuyen - Do an CNTT-KLCN108

## Cau truc project

- AdminWeb/       - Web Admin (ASP.NET Core Razor Pages)
- DispatcherWeb/  - Web Dieu phoi (ASP.NET Core Razor Pages)
- CustomerWeb/    - Website Khach hang (ASP.NET Core Razor Pages)
- CustomerApp/    - App Khach hang (Flutter)
- DriverApp/      - App Tai xe (Flutter)
- Backend/        - RESTful API (ASP.NET Core Web API)
- Database/       - SQL Server scripts

## Database (SQL Server)

Chay lan luot trong SSMS hoac sqlcmd:

1. `Database/01_CreateDatabase.sql` - Tao DB CarRentalDB
2. `Database/02_CreateTables.sql` - Tao bang, FK, index
3. `Database/03_SeedData.sql` - Du lieu mau

### Bang chinh

| Bang | Mo ta |
|------|-------|
| Roles, Users | Phan quyen: Admin, Dispatcher, Customer, Driver |
| Customers, Drivers | Ho so mo rong theo vai tro |
| VehicleTypes, Vehicles | Loai xe va xe trong doi |
| Bookings | Don dat thue xe du lich |
| TripAssignments | Dieu phoi gan tai xe + xe cho don |
| Payments, Reviews | Thanh toan va danh gia |

### Tai khoan mau (mat khau: Password123! - se cap nhat khi co Auth)

- admin@carrental.vn (Admin)
- dispatcher@carrental.vn (Dispatcher)
- customer1@gmail.com, customer2@gmail.com (Customer)
- driver1@carrental.vn, driver2@carrental.vn (Driver)

## Backend API

Chay Backend:

```bash
cd Backend
dotnet run
```

OpenAPI: `/openapi/v1.json` (Development)

### Endpoint chinh

| Method | Endpoint | Role | Mo ta |
|--------|----------|------|-------|
| POST | `/api/auth/login` | All | Dang nhap, nhan JWT |
| POST | `/api/auth/register` | Guest | Dang ky khach hang |
| GET | `/api/vehicle-types` | Public | Danh sach loai xe |
| GET | `/api/vehicles` | Public | Danh sach xe |
| POST | `/api/bookings` | Customer | Dat xe |
| GET | `/api/bookings` | Customer/Dispatcher/Admin | Danh sach don |
| POST | `/api/dispatch/bookings/{id}/confirm` | Dispatcher | Xac nhan don |
| POST | `/api/dispatch/bookings/{id}/assign` | Dispatcher | Phan cong tai xe |
| GET | `/api/drivers/me/trips` | Driver | Chuyen cua tai xe |

Connection string: `appsettings.json` -> `DefaultConnection`

## Luu y

Project duoc tao tai: C:\Users\hoang\Documents\DoAnCuNhan\CarRentalSystem
(Vi thu muc D:\DoAnCuNhan khong co quyen ghi)
