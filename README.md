# Car Rental System

Hệ thống cho thuê xe du lịch trực tuyến — đồ án CNTT-KLCN108.

Trạng thái source **đến PHASE 3.6.3** (giá theo hình thức thuê, quote, cọc, kiểm xe, `FinalAmount`, `BookingFee`). Chi tiết phase: `Tuan 3.md`. Lệnh/port/emulator: `HUONG_DAN_CHAY_PROJECT.txt`.

## Cấu trúc project

| Thư mục | Vai trò |
|---------|---------|
| Backend/ | REST API + JWT + EF Core (mặc định SQLite `carrental.db`) |
| Backend.Tests/ | xunit: giá chốt, phí, quy tắc kiểm xe |
| PortalWeb/ | Web tổng hợp + **cổng đăng nhập web chính** (`:5180`) |
| AdminWeb/ | Web Admin (`:5258`) — không có login riêng; redirect PortalWeb |
| DispatcherWeb/ | Web điều phối (`:5206`) — **có login riêng** (role Dispatcher) |
| CustomerWeb/ | Web khách (`:5162`) — login qua PortalWeb; vẫn có Register |
| CustomerApp/ | Flutter: khách hàng và tài xế (`RoleRouter`) |
| DriverApp/ | Flutter: chỉ tài xế |
| Database/ | Script SQL Server (không bắt buộc khi chạy SQLite) |

Mặc định: `Backend/appsettings.json` → `DatabaseProvider: Sqlite`, `Data Source=carrental.db`. Không dùng EF Migration (`EnsureCreated` + `ALTER`/`CREATE TABLE IF NOT EXISTS`).

## Bảng tổng hợp chức năng (từ source)

| Thành phần | Vai trò | Chức năng chính |
|------------|---------|-----------------|
| CustomerApp | Khách; tài xế nếu RoleRouter | Đăng ký/đăng nhập; chọn WithDriver/SelfDrive; **quote** rồi đặt xe; đơn; **thanh toán cọc**; đánh giá WithDriver Completed; tài xế: chuyến, nhận/bắt đầu/hoàn thành (không gửi km). Admin/Dispatcher bị từ chối trên app. |
| DriverApp | Tài xế | Đăng nhập; danh sách + chi tiết chuyến; nhận/bắt đầu/hoàn thành (body trống); trạng thái Available/Busy/Offline. Không đăng ký, không đặt xe, không cọc. |
| PortalWeb | Web tổng hợp + login | Login/register/logout + route theo role. Customer: trang chủ, đặt xe (có hình thức), xem đơn. Admin: dashboard, CRUD xe, xem/hủy đơn. Dispatcher: xác nhận, gán xe (SelfDrive không tài xế), **giao xe / hoàn thành trả xe** SelfDrive. Không UI quote, không UI cọc, không UI phí/km. |
| AdminWeb | Quản trị (site riêng) | Dashboard, CRUD xe, xem/hủy đơn, logout. Chưa login → PortalWeb. |
| DispatcherWeb | Điều phối (site riêng) | Login Dispatcher; xác nhận; phân công; giao/trả xe SelfDrive. |
| CustomerWeb | Khách (site riêng) | Register (cookie local); loại xe; đặt xe (có hình thức); xem đơn; đánh giá Completed (API cần TripAssignment). Login → PortalWeb. Không quote UI, không cọc. |
| Backend | REST API | Auth JWT; xe; đơn + **quote**; đánh giá; điều phối + handover/complete SelfDrive; tài xế/chuyến; **Payment Deposit**; inspection + fee **nội bộ** lúc complete. Không mark-paid, không gateway, không CRUD phí/inspection công khai. |
| Database | Lưu trữ | Roles/Users, Customers, Drivers, VehicleTypes, Vehicles, Bookings (snapshot + `FinalAmount` + `RentalMode`), TripAssignments, BookingStatusHistory, Payments (`PaymentType`), Reviews, **VehicleInspections**, **BookingFees**. Không RefreshToken / DriverVehicle. |

## CHỨC NĂNG CỦA TỪNG THÀNH PHẦN

Chỉ liệt kê chức năng có trong source.

### CustomerApp

Nguồn: `CustomerApp/lib/screens/`, `services/api_service.dart`, `navigation/role_router.dart`, `data/portal_content.dart`.

- Đăng ký, đăng nhập, đăng xuất, khôi phục phiên
- Trang chủ: loại xe API; tuyến/FAQ tĩnh; chọn hình thức thuê
- `GET /api/bookings/quote` trước khi xác nhận đặt; POST đơn cùng `rentalMode`
- Danh sách đơn; màn chi tiết (không còn chỉ bottom sheet)
- **Cọc:** `GET /api/bookings/{id}/payments`, `POST /api/payments` (Deposit; amount do server; status Pending)
- Đánh giá khi Completed, không SelfDrive, có assignment
- RoleRouter: Customer → `CustomerShell`; Driver → `DriverTripsScreen`

Không có: danh sách xe theo biển số, mark-paid, hiển thị `finalAmount`/`fees` trên model hiện tại.

`ApiConfig.baseUrl`: Android emulator `http://10.0.2.2:5199`; web/desktop/iOS `http://localhost:5199`. Điện thoại Android thật vẫn ra `10.0.2.2` — cần đổi IP LAN.

### DriverApp

Nguồn: `DriverApp/lib/screens/` (`driver_shell`, `trips_screen`, `trip_detail_screen`, `status_screen`, `account_screen`).

- Đăng nhập chỉ role Driver; đăng xuất; khôi phục JWT
- Danh sách chuyến; chi tiết chuyến
- Nhận / bắt đầu / hoàn thành (`complete` không gửi odometer)
- Cập nhật trạng thái tài xế

Không có: đăng ký, đặt xe, cọc, đánh giá.

### PortalWeb (`:5180`)

Cookie `CarRentalPortalAuth`. Login: `http://localhost:5180/Account/Login`. Role: Admin → `/Admin`, Dispatcher → `/Dispatcher`, Customer → `/`, khác → Login.

**Customer:** trang chủ; đặt xe (WithDriver/SelfDrive); xem đơn + xe/tài xế theo mode. Không quote UI, không cọc, không đánh giá trên Portal (client có `CreateReviewAsync` nhưng không có Page gọi).

**Admin:** dashboard; CRUD xe; xem/hủy đơn (`Cancelled`).

**Dispatcher:** Pending confirm; Confirmed assign (SelfDrive không chọn tài xế); SelfDrive Assigned → Giao xe; InProgress → Hoàn thành trả xe. Gọi API không body điều kiện xe.

### AdminWeb (`:5258`)

Không `Pages/Account/Login`. Chưa login → `http://localhost:5180/Account/Login`. Cookie `CarRentalAdminAuth` không chia sẻ với PortalWeb.

Dashboard; CRUD xe; xem/hủy đơn; logout.

### DispatcherWeb (`:5206`)

**Có** `Pages/Account/Login` (chỉ Dispatcher). Chưa login → `/Account/Login`. Cookie `CarRentalDispatcherAuth`.

Xác nhận; phân công; giao xe / hoàn thành SelfDrive; logout.

### CustomerWeb (`:5162`)

Login → PortalWeb. Register còn (`CarRentalAuth`). Đặt xe có hình thức; xem đơn; đánh giá Completed.

### Backend / API

Nguồn: `Backend/Controllers/`, `Backend/Services/`. Chi tiết path: `Tuan 3.md` mục 12 hoặc `HUONG_DAN_CHAY_PROJECT.txt` mục 10.

Tóm tắt mới so với README cũ:

- Quote, snapshot giá, `PricingService`
- PaymentsController (Deposit Pending)
- Dispatch handover/complete SelfDrive
- Driver complete optional `VehicleConditionRequest`
- `BookingResponse`: snapshot, `finalAmount`, `fees`, `finalBaseAmount`, `totalFees` (`totalFees` **không** gồm ExtraKm)

`TotalAmount` = quote lúc đặt (immutable). `FinalAmount` = giá chốt sau complete (nullable). ExtraKm nằm trong `FinalAmount`; dòng `BookingFee` ExtraKm chỉ audit.

### Database

Có: Roles, Users, Customers, Drivers, VehicleTypes (cột giá theo mode), Vehicles (`CurrentKm`), Bookings, TripAssignments, BookingStatusHistory, Payments, Reviews, VehicleInspections, BookingFees.

Không có: RefreshToken, DriverVehicle.

## Chạy nhanh

```bash
cd Backend
dotnet run
```

API: `http://localhost:5199` — thử `GET /api/vehicle-types`.

```bash
cd PortalWeb
dotnet run
```

Login web: `http://localhost:5180/Account/Login`

Flutter (Backend đã chạy):

```bash
cd CustomerApp
flutter pub get
flutter run
```

Tài khoản demo (mật khẩu `Password123!`): `admin@carrental.vn`, `dispatcher@carrental.vn`, `customer1@gmail.com`, `driver1@carrental.vn`.

OpenAPI (Development): `/openapi/v1.json` — không có Swagger UI.

Tests Backend: `dotnet test Backend.Tests/Backend.Tests.csproj -c Release`.

## Database (SQL Server — tùy chọn)

Chỉ khi đổi `DatabaseProvider` sang SqlServer. Chạy lần lượt `Database/01_CreateDatabase.sql` → `02_CreateTables.sql` → `03_SeedData.sql`.
