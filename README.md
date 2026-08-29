# Car Rental System

Hệ thống cho thuê xe du lịch trực tuyến — đồ án CNTT-KLCN108.

Trạng thái source **đến PHASE 3.9** (Admin quản lý giá `VehicleType`, xem payment read-only). Chi tiết phase: `Tuan 3.md`. Lệnh/port/emulator: `HUONG_DAN_CHAY_PROJECT.txt`.

## Cấu trúc project

| Thư mục | Vai trò |
|---------|---------|
| Backend/ | REST API + JWT + EF Core (mặc định SQLite `carrental.db`) |
| Backend.Tests/ | xunit: giá chốt, phí, kiểm xe, Admin giá/payment (isolated) |
| PortalWeb/ | Web tổng hợp + **cổng đăng nhập web chính** (`:5180`) |
| AdminWeb/ | Web Admin (`:5258`) — không có login riêng; redirect PortalWeb |
| DispatcherWeb/ | Web điều phối (`:5206`) — **có login riêng** (role Dispatcher) |
| CustomerWeb/ | Web khách (`:5162`) — login qua PortalWeb; vẫn có Register |
| CustomerApp/ | Flutter: khách hàng và tài xế (`RoleRouter`) |
| DriverApp/ | Flutter: chỉ tài xế |
| Database/ | Script SQL Server (không bắt buộc khi chạy SQLite) |

Mặc định: `Backend/appsettings.json` → `DatabaseProvider: Sqlite`, `Data Source=carrental.db`. Không dùng EF Migration (`EnsureCreated` + `ALTER`/`CREATE TABLE IF NOT EXISTS`).

Đặc tả hiện trạng (flow, action, state, API matrix): `Tuan 3.md` mục 18 trở đi. Đặc tả này **chỉ** mô tả source đã code.

Trạng thái: **Đã triển khai** = API + UI gọi được. **Có API, chưa có UI** = endpoint tồn tại, không có màn hình gọi. **CHƯA TRIỂN KHAI** = không có trong source.

## 1. Bảng chức năng toàn hệ thống (source)

| Thành phần | Role | Chức năng (thực tế trong source) | API | Trạng thái |
|------------|------|--------------------------------|-----|------------|
| Backend | — | JWT login/register Customer; GET me | `POST /api/auth/login`, `POST /api/auth/register`, `GET /api/auth/me` | Đã triển khai |
| Backend | Anonymous | Catalog loại xe (chỉ pricePerDay/Km, IsActive); list/chi tiết xe | `GET /api/vehicle-types`, `GET /api/vehicles`, `GET /api/vehicles/{id}` | Đã triển khai |
| Backend | Admin | CRUD xe | `POST/PUT/DELETE /api/vehicles` | Đã triển khai |
| Backend | Admin | Xem/sửa 8 giá VehicleType; không sửa TypeName/Booking | `GET/PUT /api/admin/vehicle-types`, `GET .../{id}` | Đã triển khai |
| Backend | Customer | Quote; tạo đơn (snapshot + TotalAmount server); list/chi tiết đơn mình | `GET /api/bookings/quote`, `POST /api/bookings`, `GET /api/bookings`, `GET /api/bookings/{id}` | Đã triển khai |
| Backend | Customer | Review Completed + có TripAssignment | `POST /api/bookings/{id}/reviews` | Đã triển khai |
| Backend | Customer | Deposit Pending (amount = QuotedDepositAmount); list payment đơn mình | `POST /api/payments`, `GET /api/bookings/{id}/payments` | Đã triển khai |
| Backend | Admin | List payment (lọc bookingId/status/paymentType); không tạo/sửa | `GET /api/admin/payments` | Đã triển khai |
| Backend | Admin, Dispatcher | PATCH status (không kiểm tra transition trong service) | `PATCH /api/bookings/{id}/status` | Đã triển khai |
| Backend | Dispatcher | Confirm Pending→Confirmed; Assign; Handover/Complete SelfDrive | `POST /api/dispatch/bookings/{id}/confirm\|assign\|handover\|complete` | Đã triển khai |
| Backend | Driver | me, trips, status, accept/start/complete (+ optional Return) | `GET /api/drivers/me`, `.../trips`, `PATCH .../status`, `POST .../accept\|start\|complete` | Đã triển khai |
| Backend | Admin, Dispatcher | List tài xế | `GET /api/drivers` | Đã triển khai |
| Backend | Customer (chủ), Admin, Dispatcher | GET inspections; **Driver 403** | `GET /api/bookings/{id}/inspections` | Đã triển khai |
| Backend | — | Inspection/Fee **không** có POST công khai; tạo nội bộ lúc handover/complete | — | Không có API CRUD công khai |
| CustomerApp | Customer | Register, login, logout, getMe; RoleRouter chặn Admin/Dispatcher | auth/* | Đã triển khai |
| CustomerApp | Customer | Home loại xe; chọn mode; quote; đặt xe; list/chi tiết; snapshot/Total/Final/fees/inspections (từ BookingResponse); cọc Deposit; review WithDriver Completed | quote, bookings, payments, reviews | Đã triển khai |
| CustomerApp | Driver | DriverTripsScreen: accept/start/complete **không body** | drivers/trips/* | Đã triển khai |
| DriverApp | Driver | Login (chỉ Driver); trips; detail; accept/start; complete **form Return 4 field optional**; status; getMe | auth, drivers/* | Đã triển khai |
| PortalWeb | Customer | Login/register; home; đặt xe **có quote**; list/chi tiết; **cọc**; xem fees/inspections. **Không** trang Review | quote, bookings, payments, inspections | Đã triển khai (trừ Review UI) |
| PortalWeb | Admin | Dashboard; CRUD xe; Giá; Payments read-only; list đơn; hủy Cancelled | vehicles, admin/vehicle-types, admin/payments, bookings, PATCH status | Đã triển khai |
| PortalWeb | Dispatcher | Confirm; Assign; Handover/Complete SelfDrive **không gửi body điều kiện** | dispatch/* | Đã triển khai (form km: CHƯA có trên Portal) |
| AdminWeb | Admin | Dashboard; CRUD xe; Giá; Payments; hủy đơn. **Không Login**; **không Page gọi SetAuth** | cùng API Admin | Có UI; session cookie: CHƯA CÓ trong source |
| DispatcherWeb | Dispatcher | Login riêng; Confirm; Assign; form Handover/Complete 4 field | dispatch/* + VehicleConditionRequest | Đã triển khai |
| CustomerWeb | Customer | Register local; Login **redirect Portal**; home loại xe; quote+đặt; list/chi tiết/cọc/fees/inspections; Review Completed | quote, bookings, payments, inspections, reviews | Đã triển khai |
| Database | — | SQLite `carrental.db` + EnsureCreated + ALTER idempotent; script SQL Server `Database/` | — | Đã triển khai |

**CHƯA TRIỂN KHAI (source):** mark-paid; Refund; Balance; settlement; gateway MoMo/VNPay; OTP/email; GPS; CRUD VehicleType (tạo/xóa); đơn giá Late/Fuel/Damage; Admin tạo payment; POST inspection/fee công khai; RefreshToken; DriverVehicle.

## 2. Chức năng theo Role

**Customer:** Register (Gmail + SĐT 10 số + mật khẩu mạnh); Login; xem loại xe public; chọn WithDriver/SelfDrive; thời gian; estimatedDistance; Quote (CustomerApp + Portal Create + CustomerWeb Create); POST Booking (không gửi TotalAmount); xem list/chi tiết đơn **của mình**; xem snapshot/`TotalAmount`/`FinalAmount`/`fees`/`inspections` trên response; GET payments; POST Deposit; Review nếu Completed **và** có TripAssignment (SelfDrive API từ chối). Không Confirm/Assign/Handover. Không Admin pricing/payments. Không Driver trips.

**Driver:** Login (DriverApp bắt buộc role Driver; CustomerApp RoleRouter). GET me; PATCH status Available/Busy/Offline (Available bị chặn nếu còn chuyến mở). GET trips. Accept (assignment Assigned). Start (Assigned **hoặc** Accepted → InProgress booking+assignment). Complete (assignment InProgress) → Return inspection optional + FinalAmount. **Không** Handover. **Không** tạo booking/payment. `GET /api/bookings` list → 403. `GET /api/bookings/{id}/inspections` → 403. `GET /api/bookings/{id}`: source **không** Forbid Driver (khác list). Không `/api/admin/*`.

**Dispatcher:** Login Portal hoặc DispatcherWeb. GET bookings (mọi đơn). Confirm chỉ Pending. Assign: Vehicle bắt buộc; WithDriver bắt buộc DriverId; SelfDrive cấm DriverId, ghi AssignedVehicleId, **không** TripAssignment. Handover chỉ SelfDrive Assigned. Complete chỉ SelfDrive InProgress. UI Portal/DispatcherWeb **chỉ hiện** Confirmed để Assign (API vẫn nhận Pending hoặc Confirmed). Không PUT pricing. Không GET `/api/admin/payments`. PATCH status (cùng Admin).

**Admin:** Login **PortalWeb** → `/Admin`. AdminWeb không trang Login; **không page nào gọi `SetAuth`** → cookie `CarRentalAdminAuth` không được tạo từ UI source. Dashboard đếm xe/đơn. CRUD xe. Pricing 8 field. Payments GET. List bookings; UI hủy `Cancelled` khi Pending/Confirmed. **Không** mark Paid / Refund / Balance / gateway. **Không** dispatch confirm/assign/handover.

## 3. Catalog page Web (handler + API)

Chi tiết precondition/postcondition/DB: `Tuan 3.md` mục 31.

### PortalWeb `:5180` — cookie `CarRentalPortalAuth`

| Route | Handler | Role | API | Side effect |
|-------|---------|------|-----|-------------|
| `/Account/Login` | GET, POST | Anon | POST `/api/auth/login` | Cookie; không đổi Users |
| `/Account/Register` | GET, POST | Anon | POST `/api/auth/register` | INSERT Customer; cookie |
| `/Account/Logout` | GET | — | Không | Xóa cookie |
| `/` | GET | Anon/Customer | GET vehicle-types | Redirect Admin/Dispatcher |
| `/Customer` | GET | — | Không | Redirect `/#bang-gia` |
| `/Customer/Bookings` | GET | Customer | GET bookings | Đọc |
| `/Customer/Bookings/Create` | GET, POST Quote, POST | Customer | GET types, GET quote, POST bookings | INSERT Booking Pending |
| `/Customer/Bookings/Details/{id}` | GET, POST Deposit | Customer | GET booking, payments, inspections; POST payments | INSERT Payment Pending |
| `/Admin` | GET | Admin | GET vehicles, bookings | Đếm |
| `/Admin/Vehicles` | GET, POST Delete | Admin | GET/DELETE vehicles | DELETE nếu không TripAssignment |
| `/Admin/Vehicles/Create` | GET, POST | Admin | GET types, POST vehicles | INSERT Available; form không Color/Km |
| `/Admin/Vehicles/Edit/{id}` | GET, POST | Admin | GET/PUT vehicle | UPDATE kể cả Status/CurrentKm |
| `/Admin/Pricing` | GET | Admin | GET admin vehicle-types | Đọc |
| `/Admin/Pricing/Edit/{id}` | GET, POST | Admin | GET/PUT 8 giá | UPDATE VehicleType; không đụng Booking |
| `/Admin/Payments` | GET | Admin | GET admin payments | Đọc; không Paid |
| `/Admin/Bookings` | GET, POST Cancel | Admin | GET bookings; PATCH Cancelled | Status=Cancelled + history |
| `/Dispatcher` | GET, POST Confirm/Handover/Complete | Dispatcher | GET bookings; POST dispatch confirm/handover/complete | Handover/Complete **không body** |
| `/Dispatcher/Assign/{id}` | GET, POST | Dispatcher | GET Confirmed + drivers/vehicles Available; POST assign | TripAssignment hoặc AssignedVehicleId |

Portal **CHƯA CÓ:** Review page; form km Dispatcher; Login AdminWeb.

### DispatcherWeb `:5206`

| Route | Handler | API | Khác Portal |
|-------|---------|-----|-------------|
| `/Account/Login` | GET, POST | login | Chỉ Role Dispatcher |
| `/Account/Logout` | GET | — | → Login |
| `/` | GET, POST Confirm | GET bookings; POST confirm | Handover/Complete là **link** |
| `/Assign/{id}` | GET, POST | như Portal Assign | Chỉ Confirmed |
| `/Handover/{id}` | GET, POST | POST handover + 4 field | Pre: SelfDrive Assigned |
| `/Complete/{id}` | GET, POST | POST complete + 4 field | Pre: SelfDrive InProgress |

### CustomerWeb `:5162`

| Route | Handler | API | Ghi chú |
|-------|---------|-----|---------|
| `/` | GET | GET vehicle-types | Public |
| `/Account/Register` | GET, POST | register | Cookie local + Address optional |
| `/Account/Logout` | GET | — | → `/` |
| `/Bookings` | GET | GET bookings | Link Review nếu Completed không SelfDrive |
| `/Bookings/Create` | GET, POST Quote, POST | quote + POST bookings | Login → Portal nếu chưa cookie |
| `/Bookings/Details/{id}` | GET, POST Deposit | như Portal | |
| `/Bookings/Review/{id}` | GET, POST | GET booking; POST reviews | SelfDrive chặn trên UI |

Login page: **CHƯA CÓ**.

### AdminWeb `:5258`

UI mirror Portal Admin (dashboard, xe có Color/Km lúc tạo, giá, payments, hủy đơn). Login: **CHƯA CÓ**. `SetAuth` **không** được gọi từ Page → session AdminWeb không tạo được từ source.

## 11. API vs UI (có gọi hay không)

| Chức năng | Backend | PortalWeb | AdminWeb | CustomerApp | DriverApp | DispatcherWeb | CustomerWeb |
|-----------|---------|-----------|----------|-------------|-----------|---------------|-------------|
| Login | Có | Có | CHƯA (redirect Portal) | Có | Có (chỉ Driver) | Có (chỉ Dispatcher) | Redirect Portal |
| Register Customer | Có | Có | Không | Có | Không | Không | Có |
| GET /auth/me | Có | Không gọi | Không | Có (Account) | Có (Account) | Không | Không |
| Catalog vehicle-types | Có (DTO hẹp) | Home, đặt xe | Dropdown xe | Home, đặt xe | Không | Không | Home, đặt xe |
| Admin pricing PUT | Có | `/Admin/Pricing` | `/Pricing` | Không | Không | Không | Không |
| Quote | Có (Customer) | Create booking | Không | Create booking | Không | Không | Create booking |
| Tạo booking | Có | Có | Không | Có | Không | Không | Có |
| Cọc Deposit | Có | Details | Không | Chi tiết | Không | Không | Details |
| Admin GET payments | Có | `/Admin/Payments` | `/Payments` | Không | Không | Không | Không |
| Confirm/Assign | Có | `/Dispatcher` | Không | Không | Không | Index/Assign | Không |
| Handover/Complete SelfDrive + form km | API optional body | Nút, **không body** | Không | Không | Không | **Form 4 field** | Không |
| Driver accept/start/complete | Có | Không | Không | DriverTrips **complete không body** | **complete có form optional** | Không | Không |
| Review | Có (cần TripAssignment) | Client có, **không Page** | Không | Chi tiết WithDriver | Không | Không | `/Bookings/Review` |
| GET inspections | Có | Details | Không | Qua BookingResponse | Không (403) | Không UI | Details |
| Mark-paid / Refund / gateway | CHƯA | Không | Không | Không | Không | Không | Không |

Luồng màn hình chi tiết, từng page/handler, action HTTP, state, API matrix, lỗi, legacy #1/#2: `Tuan 3.md` mục 18–31.

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
