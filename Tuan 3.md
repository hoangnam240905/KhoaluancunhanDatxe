# Tuần 3 — Giá, cọc, kiểm xe, phí phát sinh, Admin giá/thanh toán

Trạng thái source **đến PHASE 3.9** (Admin quản lý giá `VehicleType` + xem payment read-only).

File này dựa trên source (Backend, Flutter, web, `Database/`, `Backend.Tests`). Hướng dẫn chạy: `HUONG_DAN_CHAY_PROJECT.txt`. Tổng quan repo: `README.md`.

## 1. Phạm vi tuần

Đã làm (Backend là trọng tâm; UI chỉ khi phase đó yêu cầu):

| Phase | Nội dung | UI |
|-------|----------|----|
| 3.1 | Snapshot giá trên `Booking` lúc tạo đơn | Không |
| 3.2 | `PricingService` — nguồn giá duy nhất | Không |
| 3.3 | `GET /api/bookings/quote`; CustomerApp confirm dùng quote | CustomerApp form đặt xe |
| 3.4 | Giá theo `RentalMode` (WithDriver / SelfDrive) | Form đặt xe (app + web) |
| 3.5.1 | `Payment` + API cọc | Không (API only) |
| 3.5.2 | UI thanh toán cọc | CustomerApp; sau 3.7 thêm Portal/CustomerWeb Details |
| 3.6.1 | `VehicleInspection` Handover / Return | Portal: nút không form km; 3.8 DispatcherWeb form |
| 3.6.2 | `FinalAmount` từ snapshot + km thực tế | Không UI thêm |
| 3.6.3 | `BookingFee`; ExtraKm audit; Late/Fuel/Damage chưa có đơn giá | Không UI phí |
| 3.6.4 | GET inspection trên booking | Customer/Portal/CustomerWeb đọc |
| 3.7 | UI khách: quote/final/fees/inspection/cọc (Portal/CustomerWeb/CustomerApp) | Web + app khách |
| 3.8 | Form Handover/Complete SelfDrive (DispatcherWeb); Return form DriverApp | DispatcherWeb + DriverApp |
| 3.9 | Admin sửa giá `VehicleType`; Admin xem payment | PortalWeb Admin + mirror AdminWeb |

Không làm trong tuần này: OTP/email, EF Core Migration, reset `carrental.db`, cổng thanh toán, mark-paid, Balance/Refund, CRUD phí công khai.

## 2. Hai hình thức thuê (nền PHASE 2, dùng xuyên PHASE 3)

`RentalModes`: `WithDriver` | `SelfDrive`. Body/query trống → `WithDriver`. Giá trị khác → 400.

| | WithDriver | SelfDrive |
|--|------------|-----------|
| Phân công | Dispatcher gán **tài xế + xe** → `TripAssignment` | Dispatcher gán **chỉ xe** (`AssignedVehicleId`). Cấm `DriverId`. **Không** tạo `TripAssignment` |
| Bắt đầu | Tài xế `POST .../start` | Dispatcher `POST /api/dispatch/bookings/{id}/handover` |
| Kết thúc | Tài xế `POST .../complete` | Dispatcher `POST /api/dispatch/bookings/{id}/complete` |
| Đánh giá | Được (cần `TripAssignment`) | API từ chối (không có assignment) |

Luồng trạng thái đơn: `Pending` → `Confirmed` → `Assigned` → `InProgress` → `Completed` (hoặc `Cancelled`).

## 3. PHASE 3.1 — Price snapshot

Khi `POST /api/bookings`, `BookingService` gọi `PricingService.CalculateQuote` rồi ghi:

- `TotalAmount` = tổng báo giá lúc đặt (**không đổi** sau này)
- `QuotedPricePerDay`, `QuotedPricePerKm`, `QuotedDays`
- `QuotedDriverFeePerDay`, `QuotedSelfDriveIncludedKmPerDay`, `QuotedSelfDriveExtraKmPrice`
- `QuotedDepositAmount`

Đơn seed cũ (không snapshot) giữ cột NULL. **Không backfill.** `GET` không tính lại giá từ `VehicleType` hiện tại.

SQLite: `EnsureSqliteBookingPriceSnapshotColumns` + `EnsureSqliteBookingModeSnapshotColumns` (`ALTER TABLE` idempotent).

## 4. PHASE 3.2 — `PricingService`

File: `Backend/Services/PricingService.cs`. Đăng ký scoped trong `Program.cs`.

**Quote** (`CalculateQuote`) đọc `VehicleType` hiện tại:

- Ngày thuê: `max(1, ceil(end − start))`
- SelfDrive: `rental = SelfDrivePricePerDay × ngày`; `includedKm = SelfDriveIncludedKmPerDay × ngày`; `extraKm = max(0, distance − includedKm)`; `total = rental + extraKm × ExtraKmPrice`; `driverAmount = 0`; cọc = `SelfDriveDepositAmount ?? 0`
- WithDriver: `rental = PricePerDay × ngày`; `driver = DriverFeePerDay × ngày`; `distanceAmount = distance × PricePerKm`; `total = rental + driver + distanceAmount`; cọc = `WithDriverDepositAmount ?? 0`

Cột mode-pricing trên `VehicleType` nếu NULL khi start: `FillVehicleTypePricingDefaults` (không đụng Bookings/Payments):

- `DriverFeePerDay` = 50% `PricePerDay` (làm tròn)
- `SelfDrivePricePerDay` = `PricePerDay`
- `SelfDriveIncludedKmPerDay` = 200
- `SelfDriveExtraKmPrice` = `PricePerKm`
- `WithDriverDepositAmount` = `PricePerDay`
- `SelfDriveDepositAmount` = `PricePerDay × 5`

`GET /api/vehicle-types` **không** trả các cột mode-pricing (chỉ `pricePerDay` / `pricePerKm`). Báo giá đúng mode lấy từ quote API.

**Final (3.6.2/3.6.3):** `CalculateFinalAmount` / `CalculateFinalBreakdown` chỉ dùng **snapshot trên Booking**, không đọc lại rate `VehicleType`. Thiếu snapshot → `FinalAmount` null.

## 5. PHASE 3.3 — Quote API

```
GET /api/bookings/quote
Authorize: Customer
Query: vehicleTypeId, startDate, endDate, estimatedDistance?, rentalMode?
```

Thiếu loại xe / thời gian → 400. Mode không hợp lệ → 400.

Response (`BookingQuoteResponse`): `vehicleTypeId`, `vehicleTypeName`, `rentalMode`, ngày, `quotedPricePerDay`, `quotedPricePerKm`, `quotedDays`, `estimatedDistance`, `rentalAmount`, `distanceAmount`, `totalAmount`, `driverAmount`, `includedKm`, `extraKm`, `extraKmPrice`, `depositAmount`.

CustomerApp `create_booking_screen.dart`: gọi quote trước khi xác nhận; POST booking cùng `rentalMode` / khoảng cách. PortalWeb và CustomerWeb **không** gọi quote — POST booking, Backend tự tính (cùng `PricingService`).

## 6. PHASE 3.4 — Giá theo RentalMode

Công thức như mục 4. Extra km SelfDrive chỉ phần vượt hạn mức; WithDriver tính **toàn bộ** km × `PricePerKm`.

Web đặt xe (PortalWeb, CustomerWeb) có chọn hình thức. Dispatcher/Portal Dispatcher: WithDriver hiện chọn tài xế; SelfDrive chỉ chọn xe, nút «Gán xe».

## 7. PHASE 3.5.1 — Payment model + API

Entity `Payment` thêm `PaymentType` (nullable). Hằng:

- Type: `Deposit` | `Balance` | `Refund`
- Method: `Cash` | `BankTransfer` | `MoMo` | `VNPay`
- Status: `Pending` | `Paid` | `Failed` | `Refunded`

API (`PaymentsController`, role **Customer**):

| Method | Path | Việc |
|--------|------|------|
| POST | `/api/payments` | Tạo **Deposit**. Amount **server** lấy `QuotedDepositAmount`. Status = `Pending`. `PaidAt` = null. Body **không** gửi amount |
| GET | `/api/bookings/{id}/payments` | Danh sách payment của đơn; chỉ chủ đơn |

Body tạo: `bookingId`, `paymentType`, `method`, `transactionRef?` (max 100).

Từ chối: không phải chủ đơn (403), không có snapshot cọc, đã có Deposit Pending/Paid, `Balance` / `Refund` («chưa được hỗ trợ»), method/type sai.

**Chưa có:** mark-paid, webhook, cổng MoMo/VNPay thật, Refund/Balance. Admin xem payment: PHASE 3.9.

Seed: đơn #2 có payment BankTransfer Paid 2.240.000, `PaymentType` null (seed cũ). Không backfill.

SQLite: `EnsureSqlitePaymentTypeColumn`.

## 8. PHASE 3.5.2 — Payment UI (CustomerApp)

`booking_detail_screen.dart`: load `GET .../payments`; nếu chưa có Deposit Pending/Paid và có `quotedDepositAmount` → form chọn method, tùy chọn mã giao dịch, `POST /api/payments` (không gửi amount). Hiển thị status Pending/Paid/Failed/Refunded.

Không có UI payment **tạo cọc** trên PortalWeb / CustomerWeb / AdminWeb / DispatcherWeb / DriverApp. Admin **xem** payment: PHASE 3.9.

CustomerApp **không** parse `finalAmount` / `fees` trên model `Booking` (API đã trả; app chưa hiện).

## 9. PHASE 3.6.1 — VehicleInspection

Bảng `VehicleInspections` (1 booking có tối đa 1 Handover + 1 Return — chặn trùng ở application):

`InspectionId`, `BookingId`, `VehicleId`, `InspectionType` (`Handover`|`Return`), `ActualAt` (= `DateTime.UtcNow`), `OdometerKm?`, `FuelLevel?` (0–100), `Condition?` (max 100), `Notes?` (max 500), `CreatedAt`.

**Không** có GET/POST inspection công khai. Chỉ service nội bộ.

Gắn:

- Dispatcher SelfDrive `handover` → Handover, đơn `Assigned` → `InProgress`
- Dispatcher SelfDrive `complete` → Return, đơn `InProgress` → `Completed`, xe `Rented` → `Available` (nếu đang Rented)
- Driver WithDriver `complete` → Return, assignment + booking Completed, tài xế Available nếu hết chuyến mở (trừ Offline)

Body `VehicleConditionRequest` optional; `EmptyBodyBehavior.Allow` — app/web hiện gửi **không body**.

Validate: `VehicleInspectionRules` (km ≥ 0, fuel 0–100, Return km ≥ `Vehicle.CurrentKm`, Return km ≥ Handover km nếu cả hai có).

SQLite: `EnsureSqliteVehicleInspectionsTable` (`CREATE TABLE IF NOT EXISTS`).

## 10. PHASE 3.6.2 — Actual km + FinalAmount

`Booking.FinalAmount` nullable. `TotalAmount` **không** ghi đè khi complete.

Km thực tế: `Return.Odometer − Handover.Odometer` nếu đủ số. Thiếu một trong hai → dùng `EstimatedDistance` (0 nếu null).

Công thức base (trong `FinalAmount` 3.6.2, vẫn là `BaseFinalAmount` ở 3.6.3):

- SelfDrive: `QuotedPricePerDay × QuotedDays + extraKm × extraKmPrice` với `extraKm = max(0, km − includedKmPerDay × days)`
- WithDriver: `QuotedPricePerDay × days + QuotedDriverFeePerDay × days + km × QuotedPricePerKm`

WithDriver thường **không** có Handover → km thực tế thường fallback estimated.

`Vehicle.CurrentKm`: chỉ cập nhật khi Return **có** odometer; làm tròn; **không giảm**. Body trống → không đổi `CurrentKm`.

GET `BookingResponse` thêm `finalAmount` (cuối nhóm snapshot). GET **không** tính lại.

Đơn không snapshot → complete vẫn xong, `FinalAmount` = null.

## 11. PHASE 3.6.3 — BookingFee / extra charges

Entity `BookingFee` (1-N Booking): `FeeId`, `BookingId`, `FeeType`, `Description?` (max 300), `Amount` (≥ 0), `CreatedAt` (UTC).

`BookingFeeTypes`: `LateFee` | `ExtraKm` | `Fuel` | `Damage` | `Other`.  
`IsIncludedInBase` = **chỉ ExtraKm**.

`BookingFeeService.ApplyCompletionAsync` (gọi từ Dispatcher complete SelfDrive và Driver complete):

1. `PricingService.CalculateFinalBreakdown` (snapshot + km; optional handover/return time & fuel).
2. Ghi `booking.FinalAmount = breakdown.FinalAmount`.
3. Nếu `ExtraKmAmount > 0` → một dòng ExtraKm, mô tả có chữ **«đã gồm trong giá chốt»**. **Không cộng thêm** ExtraKm vào `FinalAmount`.
4. Late/Fuel: chỉ `TryAdd` khi breakdown amount > 0. Production **không** truyền `lateFeePerDay` / `fuelPrice` → amount null → **không tạo** LateFee/Fuel dù có trả muộn hay tụt nhiên liệu.
5. Damage: **không** tính. Không có bảng giá hư hỏng trên `VehicleType`. Condition «xước» không tạo phí.

`TryAddAsync`: bỏ qua amount ≤ 0; idempotent theo `(BookingId, FeeType)` (Local + DB). Không có API CRUD phí. Customer không tạo phí.

LateDays: `max(0, ceil((Return.ActualAt − Handover.ActualAt).TotalDays − QuotedDays))`. Thiếu Handover hoặc Return time → 0. WithDriver không handover → luôn 0.

FuelDrop: `handoverFuel − returnFuel`; không có đơn giá → không ra tiền.

GET `BookingResponse` (cuối record, camelCase):

- `finalAmount`
- `fees[]` (`feeId`, `feeType`, `description`, `amount`, `createdAt`) — sort `FeeId`
- `finalBaseAmount` = `FinalAmount − totalFees` (null nếu chưa chốt)
- `totalFees` = tổng amount các phí **không** `IsIncludedInBase` (loại ExtraKm)

Ví dụ SelfDrive vượt 50 km: `FinalAmount` = `FinalBaseAmount` = rental + 50×unit; `fees` = [ExtraKm]; `totalFees` = 0.

Không backfill đơn Completed trước 3.6.3. Complete lần 2 bị chặn status/duplicate Return → không nhân phí.

SQLite: `EnsureSqliteBookingFeesTable`. SQL Server script: `Database/02_CreateTables.sql`.

## 11b. PHASE 3.9 — Admin pricing + payment (read-only)

**Giá loại xe (Admin JWT):**

| Method | Path | Việc |
|--------|------|------|
| GET | `/api/admin/vehicle-types` | Danh sách loại xe, đủ 8 cột giá |
| GET | `/api/admin/vehicle-types/{id}` | Chi tiết |
| PUT | `/api/admin/vehicle-types/{id}` | Cập nhật đúng 8 giá; tất cả bắt buộc, ≥ 0, không null |

PUT **chỉ** ghi `VehicleTypes`. Không sửa Booking/Payment, không chạy `FillVehicleTypePricingDefaults`, không đổi `TypeName`, không xóa loại xe.

`GET /api/vehicle-types` public **giữ DTO cũ** (`pricePerDay` / `pricePerKm` — không 6 cột mode).

Quote/đơn mới đọc giá catalog hiện tại. Đơn cũ giữ snapshot / `TotalAmount` / `FinalAmount`.

**Payment (Admin JWT, read-only):**

| Method | Path | Việc |
|--------|------|------|
| GET | `/api/admin/payments?bookingId=&status=&paymentType=` | List, sort `PaymentId` tăng. `bookingId` không tồn tại → 404 |

`PaymentType` null (legacy #1 / booking #2) serialize bình thường. Không mark-paid / Refund / Balance / tạo payment. `PaymentsController` Customer **không** đổi.

**UI:** PortalWeb Admin `/Admin/Pricing`, `/Admin/Payments` (cookie `CarRentalPortalAuth`). AdminWeb mirror `/Pricing`, `/Payments` (cookie `CarRentalAdminAuth`, vẫn không login riêng).

## 12. API Backend (toàn bộ, đến 3.9)

Auth JWT. Role ghi trong ngoặc.

**Auth**

- `POST /api/auth/login`
- `POST /api/auth/register` (Customer)
- `GET /api/auth/me` (đã login)

**Vehicles**

- `GET /api/vehicle-types` (public) — DTO cũ, không 6 cột mode-pricing
- `GET /api/vehicles`, `GET /api/vehicles/{id}` (public)
- `POST/PUT/DELETE /api/vehicles` (Admin)
- `GET /api/admin/vehicle-types`, `GET /api/admin/vehicle-types/{id}`, `PUT /api/admin/vehicle-types/{id}` (Admin)

**Bookings**

- `GET /api/bookings/quote` (Customer)
- `GET /api/bookings`, `GET /api/bookings/{id}` (Customer: đơn mình; Admin/Dispatcher: tất cả)
- `GET /api/bookings/{id}/inspections` (Customer chủ đơn / Admin / Dispatcher)
- `POST /api/bookings` (Customer)
- `POST /api/bookings/{id}/reviews` (Customer; Completed + có TripAssignment)
- `PATCH /api/bookings/{id}/status` (Admin, Dispatcher)

**Payments**

- `POST /api/payments` (Customer, Deposit)
- `GET /api/bookings/{id}/payments` (Customer, chủ đơn)
- `GET /api/admin/payments` (Admin, read-only; query `bookingId` / `status` / `paymentType`)

**Dispatch** (Dispatcher)

- `POST /api/dispatch/bookings/{id}/confirm`
- `POST /api/dispatch/bookings/{id}/assign` body `{ driverId?, vehicleId }`
- `POST /api/dispatch/bookings/{id}/handover` body optional — **chỉ SelfDrive Assigned**
- `POST /api/dispatch/bookings/{id}/complete` body optional — **chỉ SelfDrive InProgress**

**Drivers**

- `GET /api/drivers` (Admin, Dispatcher)
- `GET /api/drivers/me`, `GET /api/drivers/me/trips` (Driver)
- `PATCH /api/drivers/me/status` (Driver)
- `POST /api/drivers/trips/{assignmentId}/accept|start|complete` (Driver; complete body optional)

Không có: endpoint fee công khai, mark-paid, gateway.

## 13. Schema (SQLite mặc định + script SQL Server)

Bảng: Roles, Users, Customers, Drivers, VehicleTypes, Vehicles, Bookings, TripAssignments, BookingStatusHistory, Payments, Reviews, **VehicleInspections**, **BookingFees**.

Không có: RefreshToken, DriverVehicle, EF Migration.

Startup (`Program.cs`): `EnsureCreated` → ALTER/CREATE idempotent (RentalMode, snapshot, VehicleType pricing, PaymentType, VehicleInspections, FinalAmount, BookingFees) → `DbSeeder.Seed` (chỉ khi chưa có Roles) → `FillVehicleTypePricingDefaults` → `ReconcileOpenAssignmentResourceStatus`.

## 14. UI theo thành phần (đến 3.9)

**CustomerApp:** chọn mode trên home; quote + đặt xe; danh sách/chi tiết đơn; **cọc** trên chi tiết; đánh giá WithDriver Completed; RoleRouter Customer/Driver.

**DriverApp:** shell Chuyến / Trạng thái / Tài khoản; chi tiết chuyến; accept/start/complete; complete có form Return optional (4 field, omit trống).

**PortalWeb (:5180):** login/register chung; Customer đặt xe có mode; chi tiết đơn (quote/final/fees/inspection nếu có). Dispatcher xác nhận / gán / giao xe / hoàn thành SelfDrive. Admin: CRUD xe, hủy đơn, **Giá loại xe**, **xem thanh toán**. Cookie `CarRentalPortalAuth`.

**DispatcherWeb (:5206):** **có** `Pages/Account/Login` (chỉ role Dispatcher). Cookie `CarRentalDispatcherAuth`. Confirm, assign, form Handover/Complete SelfDrive (4 field optional).

**AdminWeb (:5258):** không login riêng; chưa login → PortalWeb. Cookie `CarRentalAdminAuth` (không chia sẻ với Portal). Dashboard; CRUD xe; xem/hủy đơn; **Giá**; **Thanh toán** (mirror, read-only).

**CustomerWeb (:5162):** login → PortalWeb; Register local cookie `CarRentalAuth`; đặt xe có hình thức; xem đơn/chi tiết; đánh giá Completed (API vẫn cần TripAssignment — SelfDrive sẽ lỗi).

## 15. Tests

`Backend.Tests` (xunit, net10.0):

- `PricingFinalAmountTests`
- `PricingFeeBreakdownTests` (ExtraKm không cộng hai lần; thiếu snapshot; late/fuel không bịa giá; type phí)
- `VehicleInspectionRulesTests`
- `VehicleInspectionReadTests`
- `AdminPricingPaymentTests` (PUT giá isolated; không đụng Booking; legacy payment)
- `AdminApiAuthTests` (401/403 `/api/admin/*`; public vehicle-types DTO cũ)

Chạy: `dotnet test Backend.Tests/Backend.Tests.csproj -c Release` (cần Backend không lock file output nếu đang `dotnet run` cùng project). Isolated tests dùng SQLite temp, không `carrental.db`.

## 16. Chưa làm (sau PHASE 3 — source không có)

- Mark-paid, cổng MoMo/VNPay, Balance, Refund, settlement `FinalAmount`
- Đơn giá LateFee / Fuel / Damage trên `VehicleType`
- API fee công khai; Admin tạo/sửa payment
- Backfill snapshot / FinalAmount / fees / `PaymentType` cho đơn cũ
- Login riêng AdminWeb / gộp cookie với PortalWeb
- Form km Handover/Complete trên **PortalWeb Dispatcher** (DispatcherWeb đã có)
- Trang Review trên PortalWeb (CustomerWeb + CustomerApp đã có)

**Đã sửa so với docs cũ:** PortalWeb/CustomerWeb **đã** có quote lúc đặt xe và UI cọc/fees/inspections trên Details (PHASE 3.7). Không còn ghi “chỉ CustomerApp”.

## 17. Tài khoản demo

Mật khẩu `Password123!`: `admin@carrental.vn`, `dispatcher@carrental.vn`, `customer1@gmail.com`, `customer2@gmail.com`, `driver1@carrental.vn`, `driver2@carrental.vn`, `driver3@carrental.vn`.

API: `http://localhost:5199`. Seed chỉ chạy trên DB trống (chưa có Roles).

---

## 18. Đặc tả hiện trạng (source-of-truth)

Mục 18–30 mô tả **code hiện tại**, không phải kế hoạch. Nếu chưa có endpoint/UI: ghi **CHƯA TRIỂN KHAI** / **CHƯA CÓ**. Bảng chức năng + Role + API vs UI: `README.md`.

### 18.1 Phân loại trạng thái triển khai

| Mã | Nghĩa |
|----|--------|
| Đã triển khai | Backend xử lý + có UI/app gọi |
| Có API, chưa có UI | Endpoint tồn tại; không có màn hình gọi (vd. Portal Dispatcher handover không gửi body; Portal không có page Review) |
| Có UI, chưa có nghiệp vụ backend | **Không gặp** trong source hiện tại (UI không mock payment/pricing) |
| CHƯA TRIỂN KHAI | Không có controller/service/page |

---

## 19. Luồng nghiệp vụ Booking

Quote **không bắt buộc** trước POST booking: server luôn `PricingService.CalculateQuote` lúc tạo. UI CustomerApp/Portal Create/CustomerWeb Create **có** gọi quote rồi mới POST.

### A. Customer WithDriver (source)

Customer Login → chọn VehicleType + **WithDriver** → StartDate/EndDate → estimatedDistance (optional) → `GET /api/bookings/quote` (UI) → xem breakdown → `POST /api/bookings` → **Pending** → Customer `POST /api/payments` Deposit → Payment **Pending** (không tự Paid) → Dispatcher Confirm → **Confirmed** → Dispatcher Assign **DriverId + VehicleId** → **Assigned** + `TripAssignment.Status=Assigned` + Vehicle Rented + Driver Busy → Driver Accept → assignment **Accepted** (Booking **vẫn Assigned**) → Driver Start (Assigned **hoặc** Accepted) → assignment + Booking **InProgress** → Driver Complete → Return inspection **optional** → `ApplyCompletionAsync` (FinalAmount) → Booking + assignment **Completed**; Vehicle Available; Driver Available nếu không Offline và không còn chuyến mở.

**Không có Driver Handover.** Driver **không** tạo Handover. Complete WithDriver chỉ tạo **Return** nếu có `AssignedVehicleId` hoặc `TripAssignment.VehicleId`.

**FinalAmount:** `PricingService.CalculateFinalBreakdown`. Nếu không đủ odometer (WithDriver thường không Handover → `ResolveActualKm` = null) → dùng `EstimatedDistance` (và `?? 0`). ExtraKm nằm trong FinalAmount; dòng BookingFee ExtraKm chỉ audit.

**Không** chuyển Payment sang Paid.

### B. Customer SelfDrive (source)

Customer Login → SelfDrive → Quote → POST Booking → Pending → Deposit Pending → Dispatcher Confirm → Assign **chỉ Vehicle** (`DriverId` null/0) → Assigned, `AssignedVehicleId`, **không** TripAssignment, Vehicle Rented → Dispatcher Handover → Booking **InProgress** + VehicleInspection **Handover** (optional 4 field) → Dispatcher Complete → Return inspection → FinalAmount → Booking **Completed**; Vehicle Available nếu đang Rented.

Ràng buộc: không TripAssignment; không Driver; không DriverApp; Handover và Return đều có thể có VehicleInspection; `Vehicle.CurrentKm` chỉ tăng khi Return có odometer hợp lệ (không giảm); FinalAmount server-side; ExtraKm không cộng hai lần (`totalFees` không gồm ExtraKm).

---

## 20. Action / Event Flow (HTTP)

Source không có message bus. Dưới đây là HTTP action.

### AUTH

**Login** — Actor: bất kỳ user. Pre: không. `POST /api/auth/login` `{email,password}`. JWT + role. 401 `"Email hoặc mật khẩu không đúng."` Logout web = xóa cookie; Flutter = xóa SharedPreferences. **Không** API logout.

**Register** — Actor: anonymous. `POST /api/auth/register`. Chỉ tạo Customer. 400: tên/Gmail/SĐT/mật khẩu (`CustomerRegistrationRules`); `"Email đã được sử dụng."` / `"Số điện thoại đã được sử dụng."` Driver/Admin/Dispatcher **không** register API.

**GET me** — `[Authorize]`. `GET /api/auth/me`. CustomerApp + DriverApp Account. Web **không** gọi.

### BOOKING

**Get Quote** — Customer. Query: vehicleTypeId, startDate, endDate, estimatedDistance?, rentalMode?. Controller thêm: typeId null/≤0 → `"Loại xe không hợp lệ."`; start/end null → `"Thời gian thuê không hợp lệ."` Service: `"Thời gian kết thúc phải sau thời gian bắt đầu."` / `"Km dự kiến không được âm."` / `"Hình thức thuê không hợp lệ."` / `"Loại xe không hợp lệ."` Không ghi DB.

**Create Booking** — Customer. Body không có TotalAmount. Snapshot + TotalAmount + Status Pending + history `"Khach tao don dat xe"`. 400 `"Dữ liệu đặt xe không hợp lệ."` nếu EndDate không sau StartDate, mode không resolve, hoặc type không active. **Không** validate estimatedDistance âm lúc create (khác quote). Web không gửi lat/lng (null).

**Get list** — Customer: đơn mình. Admin/Dispatcher: tất cả (+ filter status). Driver: **403**.

**Get by id** — Customer không phải chủ: 403. Không tìm thấy: 404. Admin/Dispatcher/Driver JWT: source **không** Forbid GET by id.

**Cancel (PATCH status)** — Admin + Dispatcher. Service gán `Status` **không** whitelist. UI Admin chỉ gửi `Cancelled` khi Pending/Confirmed. Ghi `BookingStatusHistory`.

**Review** — Customer. Rating 1–5; Status Completed; **TripAssignment bắt buộc**; một review/đơn. Controller 400 `"Không thể đánh giá đơn này."` nếu service null.

### DISPATCH (Dispatcher only; khác → 403)

**Confirm** — Pre: Pending. → Confirmed + history. 400 `"Chỉ xác nhận đơn đang chờ."`

**Assign** — Pre: Pending **hoặc** Confirmed; không Completed/Cancelled. Vehicle Required. WithDriver: DriverId bắt buộc, tạo TripAssignment Assigned, Driver Busy, Vehicle Rented. SelfDrive: DriverId > 0 → 400; set AssignedVehicleId; **không** TripAssignment. Booking → Assigned. 400 xe/tài không Available.

**Handover** — Pre: SelfDrive, Assigned, AssignedVehicleId. Tạo Handover (duplicate → `"Đơn này đã có biên bản giao xe."`). Booking → InProgress + history `"Điều phối giao xe tự lái"`. **Không** TripAssignment, Payment, FinalAmount. Không SelfDrive → `"Chỉ đơn tự lái mới dùng giao xe."` Không Assigned → `"Chỉ giao xe khi đơn đã được gán xe."`

**Complete SelfDrive** — Pre: SelfDrive InProgress + AssignedVehicleId. Return inspection; ApplyCompletionAsync; Booking Completed; xe Available nếu đang Rented; history `"Điều phối hoàn thành trả xe tự lái"`. Không SelfDrive → `"Chỉ đơn tự lái mới hoàn thành trả xe tại điều phối."` Không InProgress → `"Chỉ hoàn thành khi đơn đang trong quá trình thuê."` **Không** Payment Paid.

### DRIVER (Driver only)

**Accept** — Assignment Assigned + đúng DriverId → Accepted. Booking không đổi. Fail → `400` **không body message** (`BadRequest()`).

**Start** — Assigned **hoặc** Accepted → assignment InProgress (`StartedAt` UTC) + Booking InProgress. Fail → `400` không message.

**Complete** — Assignment InProgress. Return inspection; ApplyCompletionAsync; assignment+Booking Completed; `CompletedAt`; TotalTrips++; xe Available nếu Rented; Driver Available trừ Offline hoặc còn chuyến mở. Inspection lỗi → 400 `{message}`. Sai status/id → `400` không message. **Không** Handover.

### PAYMENT

**Create Deposit** — Customer, chủ đơn. Amount **bỏ qua** client; = QuotedDepositAmount. Status Pending; PaidAt null. Type null/không resolve → `"Loại thanh toán không hợp lệ."` Refund → `"Refund chưa được hỗ trợ ở giai đoạn này."` Balance → `"Thanh toán phần còn lại chưa được hỗ trợ ở giai đoạn này."` Method không Cash/BankTransfer/MoMo/VNPay → `"Phương thức thanh toán không hợp lệ."` TransactionRef >100 → `"Mã giao dịch không hợp lệ."` Thiếu QuotedDepositAmount → `"Đơn hàng chưa có thông tin tiền cọc."` Trùng Deposit Pending/Paid → `"Đơn này đã có khoản cọc đang chờ hoặc đã thanh toán."` Method = nhãn, **không** gọi cổng.

**GET payments booking** — Customer chủ đơn. Admin/Dispatcher: **403** (class `[Authorize(Roles=Customer)]`).

**Admin GET payments** — Admin. Query bookingId/status/paymentType. 404 `"Không tìm thấy đơn."` nếu bookingId không có. **Không** POST/PATCH.

### PRICING ADMIN

GET list/id (cả IsActive=false). PUT 8 field: null → `"Phải gửi đủ 8 giá, không được để trống."`; <0 → `"Giá không được âm."` Không sửa Booking/Payment/TypeName. Không backfill. GET booking **không** recalculate. Admin GET payments: filter status/paymentType **không** enum-check (chỉ `Trim` + so sánh chuỗi); bookingId không tồn tại → 404 `"Không tìm thấy đơn."`

Public `GET /api/vehicle-types`: **không** trả 6 giá mode.

### INSPECTION

Tạo nội bộ Handover (dispatch handover) hoặc Return (dispatch/driver complete). GET: Customer chủ / Admin / Dispatcher; Driver 403. ActualAt = UtcNow. Client **không** gửi ActualAt/InspectionType. Odo >= 0; Fuel 0–100; Return odo >= CurrentKm; Return odo >= Handover odo nếu cả hai có. Duplicate type → 400.

### FEES

Chỉ lúc complete: ExtraKm audit nếu ExtraKmAmount > 0. Late/Fuel chỉ nếu breakdown > 0 (production `CalculateFinalBreakdown` **không** truyền lateFeePerDay/fuelPrice → **không** tạo tiền Late/Fuel). Damage không tính.

---

## 21. State machine

### Booking (giá trị string trong DB)

| Từ | Action | Actor | Tới |
|----|--------|-------|-----|
| (tạo) | POST bookings | Customer | Pending |
| Pending | Confirm | Dispatcher | Confirmed |
| Pending hoặc Confirmed | Assign | Dispatcher | Assigned |
| Assigned | Handover | Dispatcher, **chỉ SelfDrive** | InProgress |
| Assigned | Driver Start | Driver (WithDriver, assignment Assigned/Accepted) | InProgress |
| InProgress | Driver Complete | Driver | Completed |
| InProgress | Dispatch Complete | Dispatcher, **chỉ SelfDrive** | Completed |
| * | PATCH status | Admin/Dispatcher | giá trị client gửi (UI: Cancelled) |

**Không** có transition code Driver Handover. Confirm **không** từ Confirmed. Assign UI chỉ Confirmed; API cho cả Pending.

### TripAssignment (chỉ WithDriver lúc Assign)

Assigned → Accept → Accepted → Start → InProgress → Complete → Completed.

Start cũng đi thẳng Assigned → InProgress (bỏ Accept). SelfDrive: **không** bảng TripAssignments.

---

## 22. Business Rules / Constraints

**Pricing:** Backend nguồn sự thật. Client không gửi TotalAmount. Snapshot lúc Create. Đổi giá VehicleType không đổi booking cũ. TotalAmount không overwrite bởi FinalAmount. FinalAmount chỉ complete server-side. GET không recalculate. Public catalog DTO không có 6 field mode. Admin PUT đủ 8 field, không null, >= 0. FillVehicleTypePricingDefaults chỉ NULL lúc startup.

**RentalMode:** WithDriver Assign bắt buộc Driver+Vehicle + TripAssignment. SelfDrive chỉ Vehicle, không Driver, không TripAssignment. Body/query trống → WithDriver. Giá trị khác → 400.

**Inspection:** Type Handover|Return. ActualAt UTC server. Không duplicate. Client không gửi type/ActualAt. CurrentKm chỉ Return + odo, không giảm.

**Payment:** Customer chỉ Deposit. Amount server. Status mới = Pending. Duplicate Deposit Pending/Paid chặn. PaymentType null legacy hợp lệ. Admin chỉ READ. Không mark Paid.

**Fees:** ExtraKm trong FinalAmount. BookingFee ExtraKm audit. totalFees không cộng ExtraKm. Late/Fuel/Damage chưa đơn giá → không tạo tiền giả.

**Review:** Completed + TripAssignment; SelfDrive không review được qua API.

**Assign/Start lệch UI:** Assign API Pending|Confirmed; Start API không bắt buộc Accept.

---

## 23. API matrix (endpoint thực tế)

| Method | Endpoint | Role | Purpose | Request | Response | Side effect | Lỗi |
|--------|---------|------|---------|---------|----------|-------------|-----|
| POST | /api/auth/login | Any | JWT | email, password | token, role, user | — | 401 sai MK |
| POST | /api/auth/register | Anon | Customer | register DTO | AuthResponse | User+Customer | 400 rules |
| GET | /api/auth/me | Auth | Profile | — | User | — | 401 |
| GET | /api/vehicle-types | Anon | Catalog hẹp | — | list | — | — |
| GET | /api/vehicles | Anon | List xe | — | list | — | — |
| GET | /api/vehicles/{id} | Anon | Chi tiết xe | — | Vehicle | — | 404 |
| POST | /api/vehicles | Admin | Tạo xe | body | Vehicle | INSERT | 400 |
| PUT | /api/vehicles/{id} | Admin | Sửa xe | body | Vehicle | UPDATE | 404 |
| DELETE | /api/vehicles/{id} | Admin | Xóa | — | — | DELETE | 400 đã dùng |
| GET | /api/admin/vehicle-types | Admin | Catalog đủ 8 giá | — | list | — | 403 |
| GET | /api/admin/vehicle-types/{id} | Admin | Một loại | — | item | — | 404 |
| PUT | /api/admin/vehicle-types/{id} | Admin | Sửa giá | 8 decimal | item | UPDATE VehicleType | 400/404 |
| GET | /api/bookings/quote | Customer | Báo giá | query | BookingQuoteResponse | — | 400 |
| POST | /api/bookings | Customer | Tạo đơn | CreateBookingRequest | BookingResponse | INSERT Booking+history | 400 |
| GET | /api/bookings | C/A/D | List | status? | list | — | Driver 403 |
| GET | /api/bookings/{id} | Auth | Chi tiết | — | BookingResponse + fees/inspections | — | 404; Customer 403 |
| PATCH | /api/bookings/{id}/status | A+D | Đổi status | Status, Note? | Booking | UPDATE+history | 404 |
| POST | /api/bookings/{id}/reviews | Customer | Review | rating, comment | Review | INSERT | 400 |
| GET | /api/bookings/{id}/inspections | C/A/D | List biên bản | — | list | — | Driver 403; C 403 |
| POST | /api/payments | Customer | Cọc | bookingId, method, type? | Payment Pending | INSERT | 400/403/404 |
| GET | /api/bookings/{id}/payments | Customer | List cọc | — | list | — | 403/404 |
| GET | /api/admin/payments | Admin | List TT | query | list | — | 404 booking |
| POST | /api/dispatch/bookings/{id}/confirm | Dispatcher | Confirm | — | Booking | status | 400 |
| POST | /api/dispatch/bookings/{id}/assign | Dispatcher | Assign | DriverId?, VehicleId | Booking | assignment/vehicle | 400 |
| POST | /api/dispatch/bookings/{id}/handover | Dispatcher | Giao SelfDrive | VehicleCondition? | Booking | Handover+InProgress | 400 |
| POST | /api/dispatch/bookings/{id}/complete | Dispatcher | Trả SelfDrive | VehicleCondition? | Booking | Return+Final+Completed | 400 |
| GET | /api/drivers | A+D | List tài | — | list | — | 403 |
| GET | /api/drivers/me | Driver | Me | — | Driver | — | 403 |
| PATCH | /api/drivers/me/status | Driver | Status | status | Driver | UPDATE | 400 Busy |
| GET | /api/drivers/me/trips | Driver | Trips | — | list | — | 403 |
| POST | /api/drivers/trips/{id}/accept | Driver | Accept | — | Assignment | Accepted | 400 |
| POST | /api/drivers/trips/{id}/start | Driver | Start | — | Assignment | InProgress | 400 |
| POST | /api/drivers/trips/{id}/complete | Driver | Complete | VehicleCondition? | Assignment | Return+Final+Completed | 400 |

**Không có:** POST mark-paid, Refund, Balance, OTP, POST inspection/fee công khai, POST VehicleType, gateway.

---

## 24. UI flow theo app

**CustomerApp** (Customer + Driver RoleRouter): Login/Register → Home (types + mode) → Create (quote → confirm POST) → Bookings → Detail (snapshot, Total, Final, fees, inspections, Deposit, Review WithDriver Completed). Driver: Trips accept/start/complete **empty body**. Admin/Dispatcher: snackbar chỉ dùng Web.

**DriverApp:** Login Driver → Trips → Detail Accept/Start/Complete **form Return optional** → Status Available/Busy/Offline → Account getMe.

**PortalWeb** `:5180` cookie `CarRentalPortalAuth`: Login → Admin `/Admin` | Dispatcher `/Dispatcher` | Customer `/`. Customer: Create quote, Details deposit+inspections. **Không** Review page. Admin: Vehicles, Pricing, Payments, Bookings cancel. Dispatcher: Index confirm/handover/complete **không body**; Assign.cshtml.

**DispatcherWeb** `:5206` cookie `CarRentalDispatcherAuth`: Login → Index Confirm → Assign → Handover/Complete **form**.

**CustomerWeb** `:5162` cookie `CarRentalAuth`: Register; Login redirect Portal; Create quote; Details deposit; Review.

**AdminWeb** `:5258` cookie `CarRentalAdminAuth`: **không Login**; redirect Portal. Mirror Admin: Dashboard, Vehicles, Pricing, Payments, Bookings cancel, Logout → Portal Login. Cookie **không** chia sẻ Portal → vào thẳng `:5258` thường chưa đăng nhập dù đã login Portal.

---

## 25. Lỗi / Authorization

**401:** thiếu/sai JWT (API); web chưa cookie → redirect login.

**403:** sai role (vd. Customer gọi dispatch/admin); Customer GET/POST payment/inspection đơn người khác; Driver GET list bookings / GET inspections; Customer/Driver `/api/admin/*`.

**404:** `"Không tìm thấy đơn."` booking; `"Không tìm thấy loại xe."`; `"Không tìm thấy xe."`; Admin payments bookingId không tồn tại.

**400 (message từ source):** quote ngày/km/mode/type; register rules; `"Không thể tạo đơn..."`; Confirm/Assign/Handover/Complete/Driver như mục 20; inspection odo/fuel/duplicate; Deposit duplicate / unsupported type; pricing null/âm; `"Không thể xóa xe đã được sử dụng."`; `"Không thể đánh giá đơn này."`; driver status Available khi còn chuyến mở.

Không bịa message khác.

---

## 26. Legacy / Regression Constraints

**Không** nằm trong code như “lock row”; đây là ràng buộc vận hành PHASE 3 (không mutate).

**Seed `DbSeeder`:** Booking 1 Customer1 WithDriver Pending, TotalAmount 6.600.000, không assignment. Booking 2 Customer2 WithDriver Confirmed 2.240.000; Payment 1 Amount 2.240.000 BankTransfer **Paid**, **PaymentType null**. Sau khi Dispatcher thao tác, **file local** có thể khác seed (vd. #1 Assigned).

**Đơn #1 / #2 (regression):** không recalculate; không backfill snapshot; không tạo inspection/payment mới; không đổi assignment; không đổi TotalAmount; không đổi FinalAmount (null). Payment #2 giữ PaymentType **null**.

**Đơn #20–#26:** không có trong `DbSeeder`. Nếu tồn tại trên `carrental.db` local (dữ liệu test PHASE 3): không recalculate, không backfill snapshot, không tạo inspection/payment mới vì test, không đổi assignment/TotalAmount/FinalAmount. **Không** mô tả field từng đơn vì không có trong source.

`FillVehicleTypePricingDefaults` chỉ cột NULL — không đụng Booking/Payment.

---

## 27. UI screens (chi tiết)

| App | Role | Screen/page | Chức năng | API | Giới hạn |
|-----|------|-------------|-----------|-----|----------|
| CustomerApp | C | login/register | Auth | login/register | Admin/D không vào app |
| CustomerApp | C | home | Types + mode | GET vehicle-types | FAQ tĩnh |
| CustomerApp | C | create booking | Quote + POST | quote, bookings | Không gửi TotalAmount |
| CustomerApp | C | booking detail | Cọc, final, fees, inspections, review | bookings, payments, reviews | Review cần assignment |
| CustomerApp | D | driver trips | Accept/start/complete | drivers/trips | Complete không body |
| DriverApp | D | trip detail | + Return form | complete + body | Không handover |
| Portal | C | Bookings/Create, Details | Quote, cọc, đọc inspection | quote, payments, inspections | Không Review page |
| Portal | A | Pricing, Payments, Vehicles, Bookings | Giá, đọc TT, CRUD xe, hủy | admin/*, vehicles, PATCH | Không mark-paid |
| Portal | D | Dispatcher Index/Assign | Confirm/assign/handover/complete | dispatch | Handover không body |
| DispatcherWeb | D | Index, Handover, Complete, Assign | + form km | dispatch + body | Không pricing/payment |
| CustomerWeb | C | Create, Details, Review | Quote, cọc, review | như Portal + reviews | Login redirect Portal |
| AdminWeb | A | mirror Portal Admin | Giá, payments, xe, hủy | admin/* | Không Login; SetAuth không gọi |

---

## 28. Điểm documentation cũ đã sửa theo source

1. PortalWeb Customer **có** quote + cọc/fees/inspections (không còn “không quote/cọc”).
2. CustomerWeb **có** quote + cọc (không còn “không gọi quote”).
3. CustomerApp **có** FinalAmount/fees/inspections trên chi tiết.
4. DriverApp complete **có** form Return optional (CustomerApp DriverTrips vẫn empty body).
5. Tuan 3 mục 16: không còn ghi “Quote UI Portal/CustomerWeb chưa có”.
6. Tuan 3.5.2 “chỉ CustomerApp” cọc: **sai** sau 3.7 — Portal + CustomerWeb Details đã gọi API.
7. Portal Dispatcher handover/complete vẫn **không** form km (khác DispatcherWeb).
8. Message quote: `"Km dự kiến không được âm."` (không phải “Quãng đường…”). Create 400: `"Dữ liệu đặt xe không hợp lệ."`
9. Handover/Complete dispatch message đúng `DispatchService` (không dùng câu cũ “Chỉ giao xe đơn tự lái đã phân công”).
10. Driver accept/start fail: `400` **không** body message.
11. Deposit: `"Đơn hàng chưa có thông tin tiền cọc."` / `"Đơn này đã có khoản cọc đang chờ hoặc đã thanh toán."` / Refund/Balance message đầy đủ.
12. Pricing PUT: `"Phải gửi đủ 8 giá, không được để trống."` Admin GET payments **không** 400 enum filter.
13. AdminWeb: `SetAuth` không được Page gọi — không còn giả định login Portal tự mở AdminWeb.
14. Assign UI chỉ Confirmed (`GetBookingsAsync("Confirmed")`); API vẫn Pending|Confirmed.
15. Xóa xe chỉ chặn `TripAssignments`, không chặn `AssignedVehicleId`.

---

## 29. PHASE 3 FINAL CHECKLIST

- [x] Pricing snapshot
- [x] Centralized PricingService
- [x] Quote API
- [x] RentalMode pricing
- [x] Deposit Payment API
- [x] Customer Deposit UI (CustomerApp + Portal Details + CustomerWeb Details)
- [x] Vehicle Inspection foundation
- [x] FinalAmount
- [x] ExtraKm breakdown
- [x] Inspection Read API
- [x] Customer UI (quote/final/fees/inspection)
- [x] Dispatcher/Driver inspection input (DispatcherWeb form; DriverApp Return form; Portal Dispatcher **chưa** form)
- [x] Admin pricing
- [x] Admin payment read-only
- [x] Tests (`Backend.Tests`)
- [x] Documentation (`README.md`, `Tuan 3.md`, `HUONG_DAN_CHAY_PROJECT.txt`)

**PHASE 3 — COMPLETED**

---

## 31. Đặc tả từng page Web (source)

Cookie **không** chia sẻ giữa site. JWT nằm trong cookie JSON (`token`, `role`, `expiresAt`). Web **không** gọi `GET /api/auth/me`.

| Site | Cookie | Login page | Ghi cookie |
|------|--------|------------|------------|
| PortalWeb `:5180` | `CarRentalPortalAuth` | `/Account/Login` | `Login` + `Register` gọi `SetAuth` |
| DispatcherWeb `:5206` | `CarRentalDispatcherAuth` | `/Account/Login` (chỉ Role==Dispatcher) | `Login.SetAuth` |
| CustomerWeb `:5162` | `CarRentalAuth` | **CHƯA CÓ** — link `http://localhost:5180/Account/Login` | Chỉ `Register.SetAuth` |
| AdminWeb `:5258` | `CarRentalAdminAuth` | **CHƯA CÓ** — redirect Portal | **Không page nào gọi `SetAuth`** |

`RolePageModel.RequireRole` (Portal): chưa login → `/Account/Login`; sai role → `RoleRoutes.HomeFor` (Admin `/Admin`, Dispatcher `/Dispatcher`, Customer `/`, khác `/Account/Login`).

Trang `Privacy`, `Error`: không API, không nghiệp vụ.

### 31.1 PortalWeb — Account

**GET/POST `/Account/Login`** — Actor: anonymous (đã login → redirect home theo role). POST: `POST /api/auth/login`. Pre: ModelState email+password. Post: cookie Portal. Redirect: Admin `/Admin`, Dispatcher `/Dispatcher`, Customer `/`. Fail: 401 message hiển thị. **Không** ghi DB (Backend chỉ đọc Users).

**GET/POST `/Account/Register`** — Anonymous. POST: `POST /api/auth/register` (phone bắt buộc trên form; address/idNumber/dob gửi null). Pre: Gmail regex, SĐT `^0\d{9}$`, StrongPassword. Post: INSERT User+Customer Role=Customer; cookie; redirect `/`. **Không** tạo Driver/Admin.

**GET `/Account/Logout`** — Xóa cookie. Không gọi API. Redirect Login. **Không** đổi DB.

### 31.2 PortalWeb — Customer

**GET `/` (`Pages/Index`)** — Anonymous hoặc Customer. Admin/Dispatcher đã login → redirect khu họ. API: `GET /api/vehicle-types` (DTO hẹp). Nội dung tĩnh `PortalContent`. **Không** đổi DB. Link đặt xe nếu Customer.

**GET `/Customer`** — Redirect `/#bang-gia`. Không API.

**GET `/Customer/Bookings`** — Role Customer. `GET /api/bookings` (JWT Customer = đơn mình). UI: mode, tuyến, TotalAmount, AssignedVehicle hoặc Assignment, link Details. **Không** Review, không Cancel, không Confirm.

**GET `/Customer/Bookings/Create`** — Customer. Query optional: typeId, pickup, dropoff, distance. `GET /api/vehicle-types`. Default RentalMode=WithDriver. **Không** quote cho đến khi bấm.

**POST handler Quote** — Cùng page. Pre: EndDate > StartDate; type thuộc list. Mode khác WithDriver/SelfDrive → chuẩn hóa WithDriver. `GET /api/bookings/quote`. Post: hiện breakdown; `QuoteConfirmed=true` + fingerprint (type|mode|start|end|distance). **Không** INSERT.

**POST (đặt xe)** — Pre: QuoteConfirmed và fingerprint khớp; không khớp → gọi quote lại, message `"Đã lấy báo giá từ máy chủ. Kiểm tra rồi bấm đặt xe."` API: `POST /api/bookings` (lat/lng null, không TotalAmount). Backend: INSERT Booking Pending + snapshot + history. Redirect Details. 400 `"Dữ liệu đặt xe không hợp lệ."`

**GET `/Customer/Bookings/Details/{id}`** — Customer. `GET /api/bookings/{id}` (403/404 → redirect Index). Payments: `GET /api/bookings/{id}/payments`. Inspections: dùng nested BookingResponse nếu có, không thì `GET .../inspections`. UI: snapshot, Total, Final, fees, inspections, form cọc. **Không** Review. **Không** PATCH status.

**POST handler Deposit** — Pre: QuotedDepositAmount != null và chưa có Deposit Pending/Paid (UI `"Không thể tạo khoản cọc cho đơn này."`). Method không thuộc Cash/BankTransfer/MoMo/VNPay → BankTransfer. `POST /api/payments` type=Deposit, **không amount**. DB: INSERT Payment Pending, Amount=QuotedDepositAmount, PaidAt null. Booking không đổi status. Không mark Paid.

**CHƯA CÓ trên Portal:** page Review (client `CreateReviewAsync` không có Page).

### 31.3 PortalWeb — Admin

**GET `/Admin`** — Admin. `GET /api/vehicles` + `GET /api/bookings`. Đếm VehicleCount, BookingCount, PendingCount. **Không** đổi DB. Link: Xe, Giá, Thanh toán, Đơn.

**GET `/Admin/Vehicles`** — `GET /api/vehicles` (không filter). UI: biển, loại, hãng, Status, Sửa, Xóa.

**POST handler Delete** — `DELETE /api/vehicles/{id}`. Pre: không có TripAssignment với VehicleId đó, không thì 400 `"Không thể xóa xe đã được sử dụng."` (kể cả xe chỉ gán SelfDrive `AssignedVehicleId` **vẫn xóa được** nếu chưa từng vào TripAssignments — source chỉ check TripAssignments).

**GET/POST `/Admin/Vehicles/Create`** — GET: `GET /api/vehicle-types` (public, IsActive). Form cshtml: TypeId, LicensePlate, Brand, Model, Year — **không** input Color/CurrentKm (default CurrentKm=0, Color null). POST: `POST /api/vehicles`. Backend Status=Available. TypeId không tồn tại → 400 rỗng. Redirect Index.

**GET/POST `/Admin/Vehicles/Edit/{id}`** — GET: types + `GET /api/vehicles/{id}` (404 nếu thiếu). Form Status: Available/Rented/Maintenance/Inactive. POST: `PUT /api/vehicles/{id}` ghi TypeId, biển, hãng, model, year, color, **Status, CurrentKm** (Admin có thể sửa km/status tay). **Không** đụng Booking.

**GET `/Admin/Pricing`** — `GET /api/admin/vehicle-types` (cả IsActive=false, đủ 8 giá). Link Sửa. Không POST.

**GET/POST `/Admin/Pricing/Edit/{id}`** — GET: `GET /api/admin/vehicle-types/{id}`. POST 8 decimal ≥0: `PUT /api/admin/vehicle-types/{id}`. DB: UPDATE 8 cột VehicleType. **Không** TypeName, không Booking, không Payment, không backfill. Redirect Index.

**GET `/Admin/Payments`** — Query bookingId, status, paymentType. `GET /api/admin/payments`. UI: bảng read-only; PaymentType null → «Thanh toán cũ». **Không** nút Paid/Refund/Balance. Filter status không validate enum trên backend (so sánh chuỗi). bookingId không có → 404 `"Không tìm thấy đơn."`

**GET `/Admin/Bookings`** — Query status optional. `GET /api/bookings`. UI: khách, tuyến, TotalAmount, Assignment (nếu có). **Không** hiện RentalMode / AssignedVehicle / FinalAmount / fees trên page này (source cshtml).

**POST handler Cancel** — Nút chỉ khi Status Pending hoặc Confirmed. `PATCH /api/bookings/{id}/status` `{status:"Cancelled", note:"Admin huy"}`. Service **không** chặn transition. DB: Booking.Status=Cancelled + history. **Không** hoàn xe/tài, không xóa Payment/inspection.

**CHƯA CÓ Portal Admin:** Confirm/Assign/Handover; tạo VehicleType; mark-paid.

### 31.4 PortalWeb — Dispatcher

**GET `/Dispatcher`** — Dispatcher. Bốn list: `GET /api/bookings?status=Pending|Confirmed`; Assigned lọc RentalMode==SelfDrive; InProgress SelfDrive.

**POST Confirm** — `POST /api/dispatch/bookings/{id}/confirm`. Pre backend: Status=Pending. DB: Confirmed + history `"Điều phối xác nhận đơn"`. Payment không đổi.

**GET/POST `/Dispatcher/Assign/{id}`** — Load **chỉ** `GET /api/bookings?status=Confirmed` rồi tìm id → Pending/Assigned = **NotFound** dù API Assign chấp nhận Pending. WithDriver: `GET /api/drivers?status=Available`. Xe: `GET /api/vehicles?status=Available` lọc TypeId đơn. SelfDrive: ẩn Driver, gửi DriverId=null. POST: `POST /api/dispatch/bookings/{id}/assign`.

WithDriver DB: INSERT TripAssignment Assigned; Driver Busy; Vehicle Rented; Booking Assigned; history. SelfDrive DB: AssignedVehicleId; Vehicle Rented; **không** TripAssignment. Lỗi source: `"Đơn có tài xế bắt buộc chọn tài xế."` / `"Đơn tự lái không được gán tài xế."` / `"Xe không khả dụng."` / `"Xe không thuộc loại xe được đặt."` / `"Tài xế không khả dụng."` / `"Tài xế đang có chuyến chưa hoàn thành."` / `"Đơn đã được phân công tài xế và xe."` / `"Đơn đã được gán xe."`

**POST Handover (Index, không form)** — `POST .../handover` **không body**. Pre: SelfDrive Assigned + AssignedVehicleId. DB: INSERT VehicleInspection Handover (odo/fuel/condition/notes null); Booking InProgress; history `"Điều phối giao xe tự lái"`. Không FinalAmount, không Payment.

**POST Complete (Index, không form)** — `POST .../complete` không body. Pre: SelfDrive InProgress. DB: INSERT Return inspection (field null); FinalAmount; BookingFee ExtraKm nếu ExtraKmAmount>0; Booking Completed; Vehicle Available nếu Rented. ResolveActualKm null (không odo) → PricingService dùng EstimatedDistance. **Không** Paid.

**CHƯA CÓ Portal Dispatcher:** form 4 field; WithDriver handover/complete; pricing; payments.

### 31.5 DispatcherWeb

**GET/POST `/Account/Login`** — POST login API; Role != Dispatcher → `"Chỉ tài khoản điều phối được đăng nhập tại đây."` (không SetAuth). Cookie Dispatcher. Index sau login **chỉ** `IsLoggedIn`, không check Role lại.

**GET `/Account/Logout`** — Xóa cookie → Login.

**GET `/` Index** — Login required. Cùng 4 list như Portal. **Confirm** POST trên Index. Assigned SelfDrive: **link** `/Handover/{id}`. InProgress: **link** `/Complete/{id}`. Confirmed: link `/Assign/{id}`.

**GET/POST `/Assign/{id}`** — Giống Portal Assign (chỉ Confirmed).

**GET/POST `/Handover/{id}`** — Pre UI: booking tồn tại, SelfDrive, Status=Assigned; không thì redirect Index. Form optional: OdometerKm≥0, Fuel 0–100, Condition≤100, Notes≤500. POST: `POST .../handover` + `VehicleConditionRequest`. Cùng side effect Portal nhưng có thể có odo/fuel. Redirect Index.

**GET/POST `/Complete/{id}`** — Pre UI: SelfDrive InProgress. Form 4 field. POST complete + body. Return + FinalAmount + Completed. Nếu Return odo: CurrentKm = round(odo) nếu ≥ CurrentKm.

**CHƯA CÓ:** pricing, payment, CRUD xe, PATCH cancel.

### 31.6 CustomerWeb

**GET `/`** — Public. `GET /api/vehicle-types`. Đăng nhập: nút Dat xe + typeId; chưa: link Portal Login.

**GET/POST `/Account/Register`** — Có thêm Address (optional). Cookie `CarRentalAuth`. Redirect `/`. **Không** đồng bộ cookie Portal.

**GET `/Account/Logout`** — Xóa cookie → `/`. Không Portal.

**GET `/Bookings`** — Login (không thì Portal Login). `GET /api/bookings`. Link Details; Review nếu Completed **và không** SelfDrive.

**GET/POST Quote + POST Create** — Giống Portal Create (query chỉ typeId). Redirect Details.

**GET Details + POST Deposit** — Giống Portal Details (cùng CanPayDeposit).

**GET/POST `/Bookings/Review/{id}`** — Login. GET booking; SelfDrive → CanReview=false, `"Đơn tự lái không đánh giá tài xế trên hệ thống hiện tại."` POST: `POST /api/bookings/{id}/reviews` rating 1–5. Backend pre: Completed + TripAssignment + chưa review + đúng Customer. Fail `"Không thể đánh giá đơn này."` DB: INSERT Review (DriverId từ TripAssignment). **Không** đổi Booking status.

**CHƯA CÓ CustomerWeb:** Login page; dispatch; Admin.

### 31.7 AdminWeb

Mọi page: `!IsLoggedIn` → `http://localhost:5180/Account/Login`. Cookie `CarRentalAdminAuth` **không** được set bởi page nào trong source (`LoginAsync` trên client **không** được Page gọi). Logout xóa cookie rồi redirect Portal Login.

Nếu cookie **đã** tồn tại (không có trong source cách tạo):

| Page | Handler | API | Ghi chú |
|------|---------|-----|---------|
| `/` | GET | vehicles + bookings count | Giống Portal Admin dashboard |
| `/Vehicles` | GET, POST Delete | GET/DELETE vehicles | Create form **có** Color + CurrentKm |
| `/Vehicles/Create` | GET/POST | POST vehicles | Status Available |
| `/Vehicles/Edit/{id}` | GET/POST | PUT vehicles | Status 4 option |
| `/Pricing` | GET | GET admin vehicle-types | |
| `/Pricing/Edit/{id}` | GET/POST | PUT 8 giá | Message validate `"Gia khong duoc am."` |
| `/Payments` | GET | GET admin payments | Read-only, filter |
| `/Bookings` | GET, POST Cancel | GET bookings; PATCH Cancelled note `"Admin huy don"` | Nút hủy Pending/Confirmed |
| `/Account/Logout` | GET | — | Không API |

**CHƯA CÓ:** Login; dispatch; Review; Customer booking create.

### 31.8 Flutter (không phải Razor; để đối chiếu)

**CustomerApp:** Splash restore JWT → RoleRouter. CustomerShell: Home / Bookings / Account. CreateBooking 5 bước + quote trước POST. Detail: payments GET/POST Deposit; fees/inspections từ Booking; Review WithDriver Completed. DriverTrips: accept/start/complete **không body**. Admin/Dispatcher: snackbar, không vào shell.

**DriverApp:** Login Role==Driver. DriverShell: Trips / Status / Account. Trip detail: accept/start; complete form 4 field optional. Không handover, không booking, không payment.

---

## 32. Xác nhận

Mục 18–31 mô tả source tại thời điểm PHASE 3.9. Không đổi API/role/state/rule. Code và `carrental.db` **không** sửa khi viết tài liệu này.
