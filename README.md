# Car Rental System

Hệ thống cho thuê xe du lịch trực tuyến — đồ án CNTT-KLCN108 (đề cương 07/09/2026).

**Code freeze.** Source cuối: PHASE A → F2, GAP 1–3, P0.1–P0.4, Driver UI, final hardening 4 gap, DemoRich. Không phát triển feature mới trên nhánh này.

Snapshot Tuần 3 + PHASE 4 Group 1–3: `Tuan 3.md`. Bổ sung A–F2 và hardening sau đó: `Tuan 4.md`. Lệnh/port: `HUONG_DAN_CHAY_PROJECT.txt`.

## Tổng quan

Sản phẩm: **Web Admin**, **Web điều phối**, **Website/App khách**, **App tài xế** — cùng một REST API và một SQLite runtime.

| Thành phần | Vai trò |
|------------|---------|
| `Backend/` | ASP.NET Core REST API + JWT + SignalR Hub + EF Core |
| `Recommender/` | Python FastAPI — chấm điểm gợi ý loại xe (**không** truy cập DB) |
| `Backend.Tests/` | xunit (isolated SQLite) |
| `PortalWeb/` | Web tổng hợp + **cổng đăng nhập web chính** (`:5180`) |
| `AdminWeb/` | Web Admin (`:5258`) — không login riêng; redirect PortalWeb |
| `DispatcherWeb/` | Web điều phối (`:5206`) — **có login riêng** (role Dispatcher) |
| `CustomerWeb/` | Web khách (`:5162`) — login qua PortalWeb; vẫn có Register |
| `CustomerApp/` | Flutter: khách hàng và tài xế (`RoleRouter`) |
| `DriverApp/` | Flutter: chỉ tài xế |
| `Database/` | Script SQL Server (không bắt buộc khi chạy SQLite) |

Mặc định: `Backend/appsettings.json` → `DatabaseProvider: Sqlite`, `Data Source=carrental.db`, `SeedDemoRich: false`. Không dùng EF Migration (`EnsureCreated` + helper `ALTER` / `CREATE TABLE IF NOT EXISTS` / unique index biển số).

Profile **Demo** (`dotnet run --launch-profile Demo`): `carrental.demo.db` + `SeedDemoRich=true`. **Không** ghi DemoRich vào `carrental.db` live.

## Kiến trúc

```
Customer / Admin / Dispatcher / Driver
        (Web Razor + Flutter App)
                ↓  HTTP REST + JWT
        ASP.NET Core Backend  (:5199)
                ↓
            SQLite
```

Gợi ý phương tiện (không phải ML):

```
ASP.NET Core  (đọc DB → snapshot JSON)
        ↓  REST POST http://127.0.0.1:8001/recommend
Python FastAPI Recommender
        ↓  hard filter + weighted scoring
    danh sách loại xe xếp hạng
```

Python **không** kết nối SQLite / `carrental.db`. Snapshot gồm loại xe, xe, lịch trống, cờ bảo trì, lịch sử type đã hoàn thành của khách (nếu đã login).

Realtime:

```
Web (realtime.js) / App native (WebSocket)
        ↓  JWT (negotiate + access_token)
SignalR Hub  /hubs/realtime
```

Sự kiện chỉ phát **sau** khi thao tác DB thành công. Client reconnect; khi nhận `ReceiveEvent` thì **reload REST**, không dùng SignalR làm nguồn dữ liệu chính. Flutter **Web** (`kIsWeb`) **không** mở WebSocket (`dart:io`).

Trạng thái trong bảng dưới: **Đã triển khai** = có trong source cuối. **Không có** = không có trong source.

---

## 1. Bảng chức năng toàn hệ thống (source cuối)

| Thành phần | Role | Chức năng (thực tế trong source) | API | Trạng thái |
|------------|------|--------------------------------|-----|------------|
| Backend | — | JWT login; register Customer + OTP Gmail; verify-email; resend OTP; forgot/reset password OTP; Google Customer login (id_token). GET me; đổi mật khẩu. Logout web = xóa cookie (không `POST /logout`) | `POST /api/auth/login`, `register`, `verify-email`, `resend-verification-otp`, `forgot-password`, `reset-password`, `google`; `GET /api/auth/login-options`, `me`; `POST /api/auth/change-password` | Đã triển khai |
| Backend | Anonymous | Catalog loại xe; list/chi tiết xe; gợi ý (gọi Python) | `GET /api/vehicle-types`, `GET /api/vehicles`, `GET /api/vehicle-types/recommended` | Đã triển khai |
| Backend | Admin | CRUD xe; giấy tờ xe (số ĐK + hạn ĐK/đăng kiểm/BH); Year 1990–2100; biển số unique NOCASE; không xóa/inactive xe đang hold/open assignment | `POST/PUT/DELETE /api/vehicles` | Đã triển khai |
| Backend | Admin | Ghi bảo trì; badge alert | `POST /api/vehicles/{id}/maintenance` | Đã triển khai |
| Backend | Admin, Dispatcher | Lịch sử bảo trì; cảnh báo km/ngày | `GET /api/vehicles/{id}/maintenance-history`; `GET /api/vehicles/maintenance-alerts` | Đã triển khai |
| Backend | Admin | CRUD loại xe + 8 giá | `GET/PUT/POST/DELETE /api/admin/vehicle-types` | Đã triển khai |
| Backend | Customer | Quote; tạo đơn (query `fromRecommendation`); list/chi tiết đơn mình | `GET /api/bookings/quote`, `POST /api/bookings`, `GET /api/bookings`, `GET /api/bookings/{id}` | Đã triển khai |
| Backend | Driver | GET booking **chỉ** khi TripAssignment.DriverId = JWT | `GET /api/bookings/{id}` | Đã triển khai (list `GET /api/bookings` → 403) |
| Backend | Customer | Review Completed **và** có TripAssignment; 1–5 sao; 1–3 sao bắt buộc comment ≤500; một review/booking | `POST /api/bookings/{id}/reviews` | Đã triển khai |
| Backend | Customer | Deposit Pending + **hold xe**; mô phỏng Paid/Failed; **không** cọc đơn Cancelled | `POST /api/payments`; `POST /api/payments/{id}/simulate-success\|simulate-failure`; `GET /api/bookings/{id}/payments` | Đã triển khai |
| Backend | Customer | Hợp đồng điện tử (Issued → Signed mô phỏng; Voided khi hủy đơn) | `POST/GET /api/bookings/{id}/contract`; `POST /api/contracts/{id}/simulate-sign` | Đã triển khai |
| Backend | Admin | List hợp đồng | `GET /api/admin/contracts` | Đã triển khai |
| Backend | Admin | List payment (lọc bookingId/status/paymentType); không tạo/sửa | `GET /api/admin/payments` | Đã triển khai |
| Backend | Admin | Dashboard (cọc Paid mô phỏng, chuyến, snapshot, attribution gợi ý) | `GET /api/admin/dashboard` | Đã triển khai |
| Backend | Admin, Dispatcher | PATCH status **chỉ** Pending/Confirmed → Cancelled | `PATCH /api/bookings/{id}/status` | Đã triển khai |
| Backend | Dispatcher | Confirm; Assign (+ overlap + **T_buffer 2h** + alternatives); Handover/Complete SelfDrive | `POST /api/dispatch/bookings/{id}/confirm\|assign\|handover\|complete` | Đã triển khai |
| Backend | Driver | me, trips, status; Accept → Start → Complete (Start **chỉ** khi Accepted) | `GET /api/drivers/me`, `.../trips`, `PATCH .../status`, `POST .../accept\|start\|complete` | Đã triển khai |
| Backend | Driver | Sự cố trên **chuyến của mình** | `GET /api/drivers/me/incidents`; `POST /api/drivers/trips/{assignmentId}/incidents` | Đã triển khai |
| Backend | Admin, Dispatcher | List sự cố / kiểm xe | `GET /api/admin/incidents`, `GET /api/admin/inspections`; `GET /api/dispatch/incidents`, `GET /api/dispatch/inspections` | Đã triển khai |
| Backend | Admin, Dispatcher | List tài xế **active** (assign) | `GET /api/drivers` | Đã triển khai |
| Backend | Admin | CRUD tài xế (soft-delete `IsActive=false`) | `GET/POST/PUT/DELETE /api/admin/drivers` | Đã triển khai |
| Backend | Admin | Customer CRUD (tạo/sửa/xem) + khóa + vô hiệu hóa (không xóa lịch sử) | `GET/POST/PUT/DELETE /api/admin/customers`, `PUT .../{id}/lock` | Đã triển khai |
| Backend | Customer (chủ), Admin, Dispatcher | GET inspections; **Driver 403** | `GET /api/bookings/{id}/inspections` | Đã triển khai |
| Backend | JWT | SignalR Hub | `/hubs/realtime` | Đã triển khai |
| CustomerApp | Customer | Register, login, logout, getMe, đổi MK; Home gợi ý; quote; đặt; cọc; simulate pay; hợp đồng; review | auth, recommended, quote, bookings, payments, contracts, reviews | Đã triển khai |
| CustomerApp | Driver | DriverTripsScreen: Accept / Start / Complete theo **assignment.status** (không body điều kiện xe) | drivers/trips/* | Đã triển khai |
| DriverApp | Driver | Login; trips; Accept rồi Start; complete **form** km/fuel/ngoại thất/kỹ thuật; báo sự cố; status; realtime native | auth, drivers/*, incidents | Đã triển khai |
| PortalWeb | Customer | Login/register; home gợi ý; đặt có quote; list/chi tiết; cọc; simulate pay; hợp đồng; **Review** WithDriver | như Customer + contract + `/Customer/Bookings/Review/{id}` | Đã triển khai |
| PortalWeb | Admin | Dashboard; CRUD xe + giấy tờ + bảo trì; Giá; Tài xế; **Customer CRUD**; Payments; Hợp đồng; list đơn; hủy | vehicles, maintenance, admin/*, dashboard, contracts | Đã triển khai |
| PortalWeb | Dispatcher | Confirm; Assign **+ alternatives khi conflict**; Handover/Complete SelfDrive **không gửi body** | dispatch/* | Đã triển khai (form km: CHƯA trên Portal) |
| AdminWeb | Admin | Mirror Admin + Inspections + Incidents + Customer CRUD. **Không Login** | cùng API Admin | Có UI; cookie `CarRentalAdminAuth` không được set từ page |
| DispatcherWeb | Dispatcher | Login riêng; Confirm; Assign + alternatives; form Handover/Complete (km/fuel/condition); Inspections; Incidents | dispatch/* + VehicleConditionRequest | Đã triển khai |
| CustomerWeb | Customer | Register local; Login **redirect Portal**; gợi ý; quote+đặt; cọc; simulate pay; hợp đồng; Review Completed | như Portal Customer + Review | Đã triển khai |
| Recommender | — | FastAPI `POST /recommend`, `GET /health` | `:8001` | Đã triển khai |
| Database | — | SQLite + EnsureCreated + ALTER idempotent; script SQL Server `Database/` | — | Đã triển khai |

**Không có trong source (limitation):** Refund/Balance/settlement runtime; gateway MoMo/VNPay thật; GPS; đơn giá Late/Fuel/Damage; Admin tạo payment; POST inspection/fee công khai; RefreshToken/JWT blacklist; DriverVehicle; chữ ký số/PKI; FCM; Redis; utilization theo giờ; CTR/impression gợi ý; login riêng AdminWeb; Customer self-cancel; Google Sign-In native trên CustomerApp Windows (dùng Portal GIS); Gmail SMTP/Google OAuth thật nếu chưa cấu hình secret.

---

## 2. Chức năng theo Role

**Customer:** Register (Gmail + SĐT 10 số + mật khẩu mạnh) → OTP Gmail → xác minh mới login được (seed/demo đã verified). Forgot/reset password bằng OTP. Google “Tiếp tục với Google” (Portal GIS, chỉ tạo/login Role Customer). Login; đổi mật khẩu; xem loại xe; **gợi ý** (ngày bắt buộc, chỗ/giá optional — Backend gọi Python); chọn WithDriver/SelfDrive; Quote; POST Booking (`fromRecommendation=true` nếu chọn từ gợi ý — flag query, client có thể gửi). Cọc Deposit (Pending) **giữ xe** nếu có `VehicleId`/hold; mô phỏng Paid/Failed; **không** tạo cọc khi đơn Cancelled. Lập/xem/ký giả lập hợp đồng. Review nếu Completed **và** có TripAssignment (SelfDrive không review). Theo dõi đơn / history qua list+chi tiết. Realtime status (web JS / app native). **Không** tự Cancel (chỉ Admin/Dispatcher PATCH).

**Driver:** Login (DriverApp bắt buộc role Driver; CustomerApp RoleRouter). GET me; PATCH status Available/Busy/Offline. GET trips **của mình**. Accept (Assigned → Accepted) / Start (**chỉ** Accepted → InProgress) / Complete. Complete trên DriverApp gửi odometer, fuel 0–100, ngoại thất, kỹ thuật, notes → Return inspection; `CurrentKm` cập nhật nếu km hợp lệ. Báo sự cố trên assignment của mình. `GET /api/bookings/{id}` chỉ khi assignment của mình; list `GET /api/bookings` → 403. Không Handover, không tạo booking/payment.

**Dispatcher:** Login Portal hoặc DispatcherWeb. Confirm Pending→Confirmed. Assign: overlap + **buffer 2 giờ**; conflict trả alternatives xe/tài. Critical section `BEGIN IMMEDIATE`. Một Driver không có nhiều open assignment (Assigned/Accepted/InProgress). Handover/Complete SelfDrive. Xem inspections/incidents (DispatcherWeb). Không PUT pricing. Không GET `/api/admin/payments` / dashboard.

**Admin:** Login **PortalWeb** → `/Admin`. Dashboard (`GET /api/admin/dashboard`). CRUD xe (giấy tờ, Year, biển số unique) / loại xe / tài xế; Customer CRUD + khóa + vô hiệu hóa; ghi bảo trì + alert; Payments GET; hợp đồng list; hủy đơn Pending/Confirmed. Xe đến hạn bảo trì **không nhận lịch mới** (hold/assign/recommend/alternative) — **không** tự đổi `Vehicle.Status`. Xe đang hold hoặc open assignment **không** xóa/vô hiệu hóa.

---

## 3. Catalog page Web (handler + API)

Chi tiết Tuần 3: `Tuan 3.md` mục 31. Bổ sung A–F2 + hardening: `Tuan 4.md`.

### PortalWeb `:5180` — cookie `CarRentalPortalAuth`

| Route | Handler | Role | API | Side effect |
|-------|---------|------|-----|-------------|
| `/Account/Login` | GET, POST, POST Google | Anon | POST `/api/auth/login`; POST `/api/auth/google`; GET `/api/auth/login-options` | Cookie; Google chỉ Customer |
| `/Account/Register` | GET, POST | Anon | POST `/api/auth/register` | INSERT Customer unverified; **không** cookie; → VerifyEmail |
| `/Account/VerifyEmail` | GET, POST, POST Resend | Anon | POST `/api/auth/verify-email`, `resend-verification-otp` | Set verified + cookie JWT |
| `/Account/ForgotPassword` | GET, POST | Anon | POST `/api/auth/forgot-password` | Không tiết lộ email tồn tại; → ResetPassword |
| `/Account/ResetPassword` | GET, POST | Anon | POST `/api/auth/reset-password` | UPDATE PasswordHash; không revoke JWT |
| `/Account/Logout` | GET | — | Không | Xóa cookie |
| `/Account/ChangePassword` | GET, POST | Đã login | POST `/api/auth/change-password` | UPDATE `PasswordHash`; không revoke JWT |
| `/` | GET | Anon/Customer | GET vehicle-types; GET recommended nếu có StartDate | Redirect Admin/Dispatcher |
| `/Customer/Bookings` | GET | Customer | GET bookings | Đọc; link Review nếu đủ điều kiện |
| `/Customer/Bookings/Create` | GET, POST Quote, POST | Customer | quote + POST bookings | INSERT Booking Pending |
| `/Customer/Bookings/Details/{id}` | GET; POST Deposit / Simulate / Contract | Customer | payments, inspections, contract, simulate-success/failure, simulate-sign | INSERT Payment Pending; hold xe; cập nhật Paid/Failed; Issued/Signed |
| `/Customer/Bookings/Review/{id}` | GET, POST | Customer | POST reviews | INSERT Review (WithDriver Completed) |
| `/Admin` | GET | Admin | GET `/api/admin/dashboard` | Đọc (không ghi DB) |
| `/Admin/Vehicles` | GET, POST Delete | Admin | vehicles; maintenance-alerts | DELETE bị chặn nếu xe occupied / đã dùng |
| `/Admin/Vehicles/Maintenance/{id}` | GET, POST | Admin | GET/POST maintenance | INSERT MaintenanceRecord |
| `/Admin/Vehicles/Create` / `Edit` | GET, POST | Admin | POST/PUT vehicles | INSERT/UPDATE + giấy tờ + Year |
| `/Admin/Pricing` + Create/Edit | GET, POST | Admin | admin vehicle-types | CRUD loại + giá |
| `/Admin/Drivers` + Create/Edit | GET, POST | Admin | admin drivers | CRUD soft-delete |
| `/Admin/Customers` + Create/Edit | GET, POST | Admin | customers CRUD + lock + deactivate | Tạo/sửa/khóa/vô hiệu hóa |
| `/Admin/Payments` | GET | Admin | GET admin payments | Đọc |
| `/Admin/Contracts` | GET | Admin | GET admin contracts | Đọc |
| `/Admin/Bookings` | GET, POST Cancel | Admin | PATCH Cancelled | Chỉ Pending/Confirmed; void contract nếu có |
| `/Dispatcher` | GET, POST Confirm/Handover/Complete | Dispatcher | dispatch | Handover/Complete **không body** |
| `/Dispatcher/Assign/{id}` | GET, POST | Dispatcher | assign; alternatives khi 409/400 conflict | TripAssignment hoặc AssignedVehicleId |

Portal **CHƯA CÓ:** form km Dispatcher; Login AdminWeb; trang Incidents/Inspections (AdminWeb/DispatcherWeb mới có).

### DispatcherWeb `:5206`

| Route | API | Ghi chú |
|-------|-----|---------|
| `/Account/Login` | login | Chỉ Role Dispatcher |
| `/` Confirm; `/Assign/{id}` | confirm/assign | Alternatives khi conflict |
| `/Handover/{id}` `/Complete/{id}` | handover/complete + 4–6 field | SelfDrive |
| `/Inspections` `/Incidents` | GET dispatch inspections/incidents | Đọc |

### CustomerWeb `:5162`

Như Portal Customer + `/Bookings/Review/{id}`. Login page: **CHƯA CÓ** (redirect Portal). Cookie `CarRentalAuth` độc lập với Portal.

### AdminWeb `:5258`

Mirror Portal Admin + `/Inspections` + `/Incidents` + Customer Create/Edit. Login: **CHƯA CÓ**. Cookie `CarRentalAdminAuth` **không** được Page nào `SetAuth`. Session độc lập với Portal.

---

## 4. API (endpoint có trong source)

**Auth:** `POST /api/auth/login`, `POST /api/auth/register` (pending OTP, không JWT), `POST /api/auth/verify-email`, `POST /api/auth/resend-verification-otp`, `POST /api/auth/forgot-password`, `POST /api/auth/reset-password`, `POST /api/auth/google`, `GET /api/auth/login-options`, `GET /api/auth/me`, `POST /api/auth/change-password`

**Catalog / gợi ý:** `GET /api/vehicle-types`, `GET /api/vehicle-types/recommended?startDate&endDate&seats&priceMax&estimatedDistance`, `GET /api/vehicles`, `GET /api/vehicles/{id}`

**Xe / bảo trì:** `POST/PUT/DELETE /api/vehicles`, `POST /api/vehicles/{id}/maintenance`, `GET /api/vehicles/{id}/maintenance-history`, `GET /api/vehicles/maintenance-alerts`

**Loại xe Admin:** `GET/POST/PUT/DELETE /api/admin/vehicle-types`

**Booking:** `GET /api/bookings/quote`, `POST /api/bookings`, `GET /api/bookings`, `GET /api/bookings/{id}`, `PATCH /api/bookings/{id}/status`, `POST /api/bookings/{id}/reviews`, `GET /api/bookings/{id}/inspections`

**Hợp đồng:** `POST /api/bookings/{id}/contract`, `GET /api/bookings/{id}/contract`, `POST /api/contracts/{id}/simulate-sign`, `GET /api/admin/contracts`

**Thanh toán (mô phỏng):** `POST /api/payments`, `GET /api/bookings/{id}/payments`, `POST /api/payments/{id}/simulate-success`, `POST /api/payments/{id}/simulate-failure`, `GET /api/admin/payments`

**Dispatch:** `POST /api/dispatch/bookings/{id}/confirm`, `.../assign`, `.../handover`, `.../complete`, `GET /api/dispatch/incidents`, `GET /api/dispatch/inspections`

**Driver:** `GET /api/drivers`, `GET /api/drivers/me`, `GET /api/drivers/me/trips`, `PATCH /api/drivers/me/status`, `POST /api/drivers/trips/{id}/accept|start|complete`, `GET /api/drivers/me/incidents`, `POST /api/drivers/trips/{id}/incidents`

**Admin khác:** `GET/POST/PUT/DELETE /api/admin/drivers`, `GET/POST/PUT/DELETE /api/admin/customers`, `PUT /api/admin/customers/{id}/lock`, `GET /api/admin/dashboard`, `GET /api/admin/incidents`, `GET /api/admin/inspections`

**Python:** `GET /health`, `POST /recommend` (chỉ Backend gọi)

**SignalR:** `POST /hubs/realtime/negotiate`, WebSocket `/hubs/realtime?id=...&access_token=...` — method client `ReceiveEvent`

OpenAPI (Development): `http://localhost:5199/openapi/v1.json` — không có Swagger UI.

---

## 5. Database

- **Live/dev:** SQLite `carrental.db` (cwd khi `dotnet run`, thường `Backend/`). Không commit. Không reset trong quy trình freeze.
- **DemoRich:** `carrental.demo.db` — chỉ khi `SeedDemoRich=true` **và** connection string chứa `carrental.demo.db` (launch profile Demo). Core `DbSeeder` vẫn chạy trước; DemoRich **append**, không thay Core seed dùng cho test.
- `EnsureCreated` lúc start. Helper idempotent: snapshot giá, PaymentType, VehicleInspections (+ Exterior/Technical), BookingFees, Users.IsLocked, Drivers.IsActive, MaintenanceRecords, SourceRecommended, Contracts, IncidentReports, **cột giấy tờ xe**, **unique index LicensePlate COLLATE NOCASE**.
- **Không** EF Migration.
- Entity `Vehicle`: `RegistrationNumber`, hạn đăng ký / đăng kiểm / bảo hiểm; `Year` required. `RegistrationNumber` unique **ở service** (không có unique index DB).

---

## 6. Business rules (source cuối đã harden)

- Graph booking: Pending → Confirmed → Assigned → InProgress → Completed; Pending/Confirmed → Cancelled. Confirm/Assign/Start/Complete đi specialized API.
- `PATCH /api/bookings/{id}/status` **không** nhảy trạng thái tùy ý — chỉ Cancelled từ Pending hoặc Confirmed.
- Driver **phải Accept** trước Start (`TripAssignment` = Accepted).
- Driver GET booking: chỉ khi có TripAssignment và `DriverId` = JWT. Không lấy DriverId từ body/query.
- Customer GET: chỉ đơn `CustomerId` của mình.
- Xe đang hold hoặc open assignment: không xóa / không Inactive.
- `LicensePlate` unique, trim, không phân biệt hoa thường (service + SQLite `COLLATE NOCASE`).
- `Vehicle.Year`: 1990 ≤ Year ≤ 2100 (Create và Update, backend 400).
- Booking Cancelled: **không** tạo Deposit, không hold mới, không success realtime.
- Maintenance-due (km ≥ 5000 **hoặc** ≥ 180 ngày, hoặc chưa có mốc completed): chặn hold / assign / recommend / alternative. Không phá trip đang chạy. Không tự set `Vehicle.Status=Maintenance`.
- Overlap + `T_buffer = 2 giờ`. Double-booking chặn bằng `SqliteWriteLock` (`BEGIN IMMEDIATE`) lúc Deposit và Assign (và Cancel status).
- Một Driver tối đa một open assignment (Assigned | Accepted | InProgress).
- Review: 1–5 sao; 1–3 sao bắt buộc comment; comment ≤ 500; một review/booking; cần Completed + TripAssignment.

---

## 7. Recommendation (không ML)

Score (Python `scoring.py`, khóa trọng số):

`0.5 * avgRating + 0.3 * bookingCountNormalized + 0.2 * availabilityBonus`

Hard filter: type active, seats, priceMax, xe usable (không Inactive/Maintenance/Rented, lịch trống, không maintenance-blocked). `estimatedDistance` nhận vào API, **không** vào score. Lịch sử khách chỉ **annotate** `reasons` (`customerHistory`), không đổi Score. `SourceRecommended` khi `POST /api/bookings?fromRecommendation=true` (query flag). Không bảng impression/click.

---

## 8. Dashboard (limitation)

`GET /api/admin/dashboard?from=&to=` (Admin). Mặc định 30 ngày UTC.

- **Tiền cọc đã Paid (mô phỏng)** = `SUM(Amount)` Payment Status=Paid và (PaymentType=Deposit **hoặc** PaymentType null). **Không** phải tổng doanh thu thuê xe. `FinalAmount` không được thu.
- Số đơn theo status; số assignment completed.
- Snapshot xe/tài (không utilization theo giờ).
- Top xe theo số chuyến completed.
- Driver: completed/cancelled/incidents / AVG review / tỷ lệ hoàn thành assignment (không dùng `Drivers.TotalTrips` seed).
- Alert bảo trì (snapshot, không lọc from/to).
- Gợi ý: **attribution** `SourceRecommended` — không CTR/conversion.

---

## 9. DemoRich

- DB riêng: `carrental.demo.db`. **Không** phải `carrental.db` live.
- Seed: `DemoRichSeeder` sau Core `DbSeeder`. Core seed **giữ nguyên** cho `Backend.Tests`.
- Có loại **Bus 29 chỗ**; dataset phong phú: booking (cả lifecycle), review, contract, maintenance, incident, inspection, recommendation candidates.
- Mỗi Driver tối đa một open assignment (phù hợp rule runtime).
- Không ghi số dòng chi tiết (số lượng seed có thể điều chỉnh trong seeder).

---

## 10. Testing

Lần chạy đầy đủ gần nhất: **`dotnet test Backend.Tests -c Release` → 385 passed / 0 failed / 0 skipped**.

Phân loại (file test, không cộng tay từng Fact):

| Nhóm | File chính |
|------|------------|
| Booking / state machine | `PhaseP0BookingStateMachineTests` |
| Dispatch / conflict / buffer | `ScheduleConflictServiceTests`, `PhaseADispatchTests` |
| Hold / payment | `PhaseAVehicleHoldTests`, `PhaseEContractPaymentTests`, `PhaseFinalCancelledDepositTests` |
| Contract | `PhaseEContractPaymentTests` |
| Recommendation | `RecommendationServiceTests`, `PhaseBRecommenderTests` |
| Maintenance | `MaintenanceAlertServiceTests`, `PhaseCMaintenanceLockTests` |
| SignalR | `PhaseDRealtimeTests` |
| Driver | `PhaseF1DriverOpsTests`, `PhaseP0DriverStartTests`, `PhaseFinalDriverBookingOwnershipTests` |
| Dashboard | `PhaseF2DashboardTests` |
| Customer CRUD | `PhaseCustomerCrudTests` |
| Vehicle legal / Year / biển số | `PhaseGap2VehicleDocumentTests`, `PhaseFinalVehicleYearTests`, `PhaseP0LicensePlateTests`, `PhaseP0VehicleDeleteProtectionTests` |
| Review | `ReviewRulesTests`, `PhaseGap3PortalReviewTests` |
| P0 / final hardening | `PhaseP0*`, `PhaseFinal*` |
| DemoRich | `PhaseDemoRichDataTests` |
| Admin / giá / inspection nền | `AdminApiAuthTests`, `AdminPhase4Group1Tests`, `AdminPricingPaymentTests`, `Pricing*`, `VehicleInspection*` |

Python: `Recommender/tests/test_recommend.py`. Flutter: vài unit test model (không E2E). `dart analyze` DriverApp: no issues; CustomerApp: 1 **info** cũ (`api_service.dart`), không phải lỗi mới.

Không ghi coverage % — repo không chạy coverage tool trong quy trình hiện tại.

Build Release Backend + AdminWeb + PortalWeb + CustomerWeb + DispatcherWeb đã xác nhận ở freeze.

---

## 11. Known limitations

Đây là giới hạn **thiết kế / phạm vi** của source cuối, không phải danh sách “bug phải sửa ngay”:

- Thanh toán cọc là **mô phỏng** (`simulate-success` / `simulate-failure`), không cổng MoMo/VNPay thật.
- Không Balance/Refund runtime (API trả 400).
- Dashboard đo **cọc Paid mô phỏng**, không phải toàn bộ doanh thu thuê.
- SelfDrive **không** review (cần TripAssignment) — rule hiện tại.
- Customer **chưa** self-cancel (chỉ Admin/Dispatcher PATCH Pending/Confirmed → Cancelled).
- AdminWeb / CustomerWeb session (cookie) độc lập với Portal; AdminWeb không có Login page.
- `RegistrationNumber` unique ở service; **chưa** unique index DB.
- `fromRecommendation` là query flag — client có thể gửi mà không đi từ UI gợi ý.
- Flutter Web **không** SignalR (`kIsWeb`).
- Utilization theo giờ **chưa** triển khai.
- Recommendation rule-based, không ML; không CTR/impression.
- Hợp đồng Issued/Signed/Voided mô phỏng — không chữ ký số.
- Không FCM, không Redis, không GPS.
- Không claim độ trễ realtime ≤ 1.5s (chưa benchmark).

---

## 12. API vs UI

| Chức năng | Backend | PortalWeb | AdminWeb | CustomerApp | DriverApp | DispatcherWeb | CustomerWeb |
|-----------|---------|-----------|----------|-------------|-----------|---------------|-------------|
| Login | Có | Có | Redirect Portal | Có | Có (Driver) | Có (Dispatcher) | Redirect Portal |
| Gợi ý Python | Có | Home | Không | Home | Không | Không | Home |
| Cọc + simulate Paid/Failed | Có | Details | Không | Chi tiết | Không | Không | Details |
| Hợp đồng + simulate-sign | Có | Details + Admin list | List | Chi tiết | Không | Không | Details |
| Buffer 2h + alternatives | Có | Assign UI | Không | Không | Không | Assign UI | Không |
| SignalR | Hub | realtime.js | realtime.js | native (không web) | native (không web) | realtime.js | realtime.js |
| Driver complete form km/fuel/exterior/technical | API | Không | Không | complete **không body** | **Có form** | Form SelfDrive | Không |
| Incident | Có | Không page | `/Incidents` | Không | Có | `/Incidents` | Không |
| Dashboard | Có | `/Admin` | `/` | Không | Không | Không | Không |
| Review | Có | `/Customer/Bookings/Review` | Không | Chi tiết WithDriver | Không | Không | `/Bookings/Review` |
| Customer CRUD | Có | `/Admin/Customers` | `/Customers` | Không | Không | Không | Không |
| Giấy tờ xe / Year | Có | Vehicles Create/Edit | Vehicles Create/Edit | Không | Không | Không | Không |
| Gateway / Refund / Balance | Không | Không | Không | Không | Không | Không | Không |

---

## Chạy nhanh

```bash
cd Backend
dotnet run
```

API: `http://localhost:5199` — `GET /api/vehicle-types`. File DB: `carrental.db`.

DemoRich (DB riêng, **không** dùng live):

```bash
cd Backend
dotnet run --launch-profile Demo
```

Gợi ý (terminal riêng, bắt buộc khi demo recommendation):

```bash
cd Recommender
py -3 -m uvicorn app:app --host 127.0.0.1 --port 8001
```

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

```bash
cd DriverApp
flutter pub get
flutter run
```

Tài khoản Core seed (mật khẩu `Password123!`): `admin@carrental.vn`, `dispatcher@carrental.vn`, `customer1@gmail.com`, `driver1@carrental.vn`. Seed/demo **không** phải xác minh email lại (`IsEmailVerified` mặc định 1).

Tests Backend: `dotnet test Backend.Tests/Backend.Tests.csproj -c Release`.

Chi tiết thứ tự, SignalR, Demo vs live DB, checklist: `HUONG_DAN_CHAY_PROJECT.txt`.

## Google Login / Email OTP / Forgot Password

Secret **không** commit. Placeholder: `Backend/appsettings.example.json`. Local: environment variables hoặc `dotnet user-secrets` (Development). `appsettings.json` giữ `Email:Enabled=false` và `SmtpPassword` rỗng.

```bash
cd Backend
dotnet user-secrets set "Email:Enabled" "true"
dotnet user-secrets set "Email:SmtpHost" "smtp.gmail.com"
dotnet user-secrets set "Email:SmtpPort" "587"
dotnet user-secrets set "Email:SmtpUsername" "YOUR_GMAIL_ADDRESS"
dotnet user-secrets set "Email:SmtpPassword" "YOUR_GMAIL_APP_PASSWORD"
dotnet user-secrets set "Email:FromEmail" "YOUR_GMAIL_ADDRESS"
dotnet user-secrets set "Email:FromName" "Car Rental"
```

`YOUR_GMAIL_APP_PASSWORD` là Gmail App Password (2FA), không phải mật khẩu Gmail thường. Không commit, không đưa vào chat/log.

| Biến môi trường | User Secrets | Ý nghĩa |
|-----------------|--------------|--------|
| `Email__Enabled` | `Email:Enabled` | `true` mới gửi Gmail SMTP |
| `Email__SmtpHost` | `Email:SmtpHost` | Mặc định `smtp.gmail.com` |
| `Email__SmtpPort` | `Email:SmtpPort` | Mặc định `587` (STARTTLS) |
| `Email__SmtpUsername` | `Email:SmtpUsername` | Gmail gửi OTP |
| `Email__SmtpPassword` | `Email:SmtpPassword` | **Gmail App Password** |
| `Email__FromEmail` / `Email__FromName` | `Email:FromEmail` / `Email:FromName` | Người gửi |
| `Email__OtpPepper` | `Email:OtpPepper` | Tùy chọn; nếu trống dùng `Jwt:Key` để HMAC OTP |
| `Google__ClientId` | `Google:ClientId` | OAuth Client ID |
| `Google__ClientSecret` | `Google:ClientSecret` | Không dùng cho GIS id_token; để trống được |

Gmail App Password: Google Account → Bảo mật → Xác minh 2 bước → Mật khẩu ứng dụng. Restart Backend sau khi set secrets. Log khởi động: `Email delivery: SMTP.` hoặc `Email delivery: disabled (NullEmailSender).`

Google OAuth (Customer, Portal): Google Cloud Console → APIs & Services → Credentials → OAuth 2.0 Client ID loại **Web**. Authorized JavaScript origins: `http://localhost:5180`. Backend `POST /api/auth/google` nhận `idToken`, validate issuer/audience/signature qua `Google.Apis.Auth`. Chỉ tạo/login **Customer**. Email trùng Admin/Dispatcher/Driver → 403. Email trùng Customer hiện có → link `GoogleSubject`, **không** ghi đè password.

Local không credential: `Email:Enabled=false` (mặc định trong repo) — OTP vẫn lưu hash, email không gửi. Tests dùng `CapturingEmailSender`, không gọi Gmail/Google thật. `GET /api/auth/login-options` trả `googleEnabled=false` nếu thiếu ClientId.

Forgot password luôn trả: `Nếu email tồn tại, mã OTP đã được gửi.` Reset: `{ email, otp, newPassword, confirmPassword }`. JWT stateless — không blacklist token cũ (giống đổi mật khẩu hiện tại).

CustomerApp Windows: nút Google hướng dẫn dùng Portal. Native Android/iOS cần thêm `google_sign_in` + google-services (chưa gắn trong repo).

AdminWeb / DispatcherWeb / DriverApp: **không** thêm Google Customer login.

## Database (SQL Server — tùy chọn)

Chỉ khi đổi `DatabaseProvider` sang SqlServer. Chạy `Database/01_CreateDatabase.sql` → `02_CreateTables.sql` → `03_SeedData.sql`. Script SQL Server **có thể chưa** gồm bảng/cột thêm bằng helper SQLite — môi trường mặc định là SQLite.
