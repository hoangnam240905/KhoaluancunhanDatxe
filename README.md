# Car Rental System

Hệ thống cho thuê xe du lịch trực tuyến — đồ án CNTT-KLCN108.

Hướng dẫn chạy chi tiết (port, lệnh, SQLite/SQL Server, emulator): xem `HUONG_DAN_CHAY_PROJECT.txt`.
File đó dựa trên source; mục không xác định được sẽ ghi rõ.

## Cấu trúc project

| Thư mục | Vai trò |
|---------|---------|
| Backend/ | REST API + JWT + EF Core (mặc định SQLite) |
| PortalWeb/ | Web tổng hợp + **cổng đăng nhập web duy nhất** (`:5180`) |
| AdminWeb/ | Web Admin riêng (`:5258`) — không còn login riêng |
| DispatcherWeb/ | Web điều phối riêng (`:5206`) — không còn login riêng |
| CustomerWeb/ | Web khách hàng riêng (`:5162`) — không còn login riêng; vẫn có Register |
| CustomerApp/ | Flutter: khách hàng và tài xế (`RoleRouter`) |
| DriverApp/ | Flutter: chỉ tài xế |
| Database/ | Script SQL Server (không bắt buộc khi chạy SQLite mặc định) |

README cũ mô tả database chính là SQL Server và thiếu PortalWeb. **Source hiện tại:** `Backend/appsettings.json` dùng `DatabaseProvider: Sqlite` và `Data Source=carrental.db`. PortalWeb tồn tại trong repo.

## Bảng tổng hợp chức năng (từ source)

| Thành phần | Vai trò | Chức năng chính |
|------------|---------|-----------------|
| CustomerApp | Khách hàng; tài xế nếu RoleRouter | Đăng ký/đăng nhập/đăng xuất; xem loại xe và tuyến tĩnh; đặt xe; xem/chi tiết đơn; tài xế: chuyến, nhận/bắt đầu/hoàn thành, đổi trạng thái. Không có đánh giá, không gọi `/api/vehicles`. |
| DriverApp | Tài xế | Đăng nhập/đăng xuất; danh sách chuyến; nhận/bắt đầu/hoàn thành; đổi trạng thái. Không có đăng ký. |
| PortalWeb | Web tổng hợp + login | Login/register/logout + route theo role. Customer: trang chủ, đặt xe, xem đơn. Admin: dashboard, CRUD xe, xem/hủy đơn. Dispatcher: xác nhận đơn, phân công tài xế+xe. Không có khu Driver; không có UI đánh giá. |
| AdminWeb | Quản trị (site riêng) | Dashboard, CRUD xe, xem/hủy đơn, logout. Login → PortalWeb. |
| DispatcherWeb | Điều phối (site riêng) | Xác nhận đơn, phân công tài xế+xe, logout. Login → PortalWeb. |
| CustomerWeb | Khách hàng (site riêng) | Register (cookie local); xem loại xe; đặt xe; xem đơn; đánh giá đơn Completed. Login → PortalWeb. |
| Backend | REST API | Auth JWT; loại xe/xe; đơn; đánh giá; điều phối; tài xế/chuyến. Không có API thanh toán. |
| Database | Lưu trữ | Roles/Users, Customers, Drivers, VehicleTypes, Vehicles, Bookings, TripAssignments, BookingStatusHistory, Payments, Reviews. Không có RefreshToken / DriverVehicle. |

Chi tiết từng mục: `HUONG_DAN_CHAY_PROJECT.txt` mục **2.1**.

## CHỨC NĂNG CỦA TỪNG THÀNH PHẦN

Chỉ liệt kê chức năng có trong source. Không ghi chức năng chỉ xuất hiện ở README cũ nếu source không có.

### CustomerApp

Nguồn: `CustomerApp/lib/screens/`, `services/api_service.dart`, `navigation/role_router.dart`, `data/portal_content.dart`, `main.dart`.

- Đăng ký, đăng nhập, đăng xuất, khôi phục phiên
- Trang chủ: loại xe từ API; tuyến phổ biến / tính năng / bước đặt / FAQ **tĩnh** (`portal_content.dart`)
- Xem thông tin loại xe (giá, chỗ) — không xem đội xe theo biển số
- Tạo đơn thuê; đặt theo tuyến phổ biến
- Danh sách đơn; chi tiết đơn (bottom sheet); xem trạng thái
- **RoleRouter:** Customer → `HomeScreen`; Driver → `DriverTripsScreen`; Admin/Dispatcher bị từ chối
- Driver trên app này: danh sách chuyến, nhận / bắt đầu / hoàn thành, trạng thái Available/Busy/Offline

Không có: đánh giá đơn, thanh toán, CRUD xe.

### DriverApp

Nguồn: `DriverApp/lib/screens/login_screen.dart`, `trips_screen.dart`, `services/api_service.dart`.

- Đăng nhập tài xế (chỉ role Driver), đăng xuất, khôi phục phiên
- Danh sách chuyến; thông tin chuyến trên danh sách
- Nhận chuyến, bắt đầu chuyến, hoàn thành chuyến
- Cập nhật trạng thái tài xế

Không có: đăng ký, đặt xe, đánh giá.

### PortalWeb (cổng đăng nhập web)

Nguồn: `PortalWeb/Pages/`, `Services/AuthSession.cs` (`RoleRoutes`), `Services/CarRentalApiClient.cs`.

**AUTHENTICATION**

- Đăng nhập: `http://localhost:5180/Account/Login`
- Đăng ký khách hàng
- Đăng xuất
- Role: Admin → `/Admin`; Dispatcher → `/Dispatcher`; Customer → `/`; khác → `/Account/Login`

**CUSTOMER**

- Trang chủ (loại xe API + nội dung tĩnh)
- Đặt xe; xem đơn và thông tin tài xế/xe nếu đã gán

Không có trang đánh giá trên PortalWeb.

**ADMIN**

- Dashboard (số xe, số đơn, đơn Pending)
- Danh sách / thêm / sửa / xóa xe
- Xem đơn; hủy đơn (`Cancelled`)

**DISPATCHER**

- Xem đơn Pending / Confirmed
- Xác nhận đơn; phân công tài xế và xe

Driver không có khu vực web.

### AdminWeb

Web Admin riêng. **Không còn** `Pages/Account/Login.*`. Chưa login → `http://localhost:5180/Account/Login`. Cookie `CarRentalAdminAuth` không chia sẻ với PortalWeb.

- Dashboard; quản lý xe (thêm/sửa/xóa/danh sách); xem đơn; hủy đơn; đăng xuất

### DispatcherWeb

Không còn login riêng. Chưa login → PortalWeb.

- Xem đơn Pending; xác nhận đơn
- Xem đơn Confirmed; phân công tài xế + xe
- Đăng xuất → PortalWeb login

### CustomerWeb

Không còn login riêng. Login → PortalWeb. **Register vẫn còn** (`Pages/Account/Register`) và ghi cookie local sau đăng ký.

- Xem loại xe; đặt xe; xem đơn
- Đánh giá đơn khi `Status == Completed`

### Backend / API

Nguồn: `Backend/Controllers/`, `Backend/Services/`.

**AUTH:** đăng nhập JWT; đăng ký khách hàng; `GET /api/auth/me` (có controller; không thấy client nào gọi).

**VEHICLES:** xem loại xe (public); xem xe / chi tiết xe (public); thêm/sửa/xóa xe (Admin). Không có API CRUD loại xe.

**BOOKINGS:** tạo đơn (Customer); xem đơn; cập nhật trạng thái (Admin/Dispatcher); đánh giá (Customer).

**DISPATCH:** xác nhận đơn; phân công tài xế + xe (Dispatcher).

**DRIVERS:** danh sách tài xế (Admin/Dispatcher); chuyến của tài xế; đổi trạng thái; nhận/bắt đầu/hoàn thành chuyến.

Không có `PaymentsController`.

### Database

Nguồn: `Backend/Entities`, `CarRentalDbContext`, `Database/02_CreateTables.sql`, `DbSeeder`.

Có: Roles, Users, Customers, Drivers, VehicleTypes, Vehicles, Bookings, TripAssignments, BookingStatusHistory, Payments, Reviews.

Không có: RefreshToken, DriverVehicle.

Payments: entity + seed; không có API thanh toán.

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

Flutter (sau khi Backend chạy; emulator Android dùng `10.0.2.2:5199` trong `lib/config/api_config.dart`):

```bash
cd CustomerApp
flutter pub get
flutter run
```

Tài khoản demo (mật khẩu `Password123!`): `admin@carrental.vn`, `dispatcher@carrental.vn`, `customer1@gmail.com`, `driver1@carrental.vn`.

OpenAPI (Development): `/openapi/v1.json` — không có Swagger UI trong source.

## Database (SQL Server — tùy chọn)

Chỉ cần nếu đổi `DatabaseProvider` sang SqlServer. Chạy lần lượt:

1. `Database/01_CreateDatabase.sql`
2. `Database/02_CreateTables.sql`
3. `Database/03_SeedData.sql`
