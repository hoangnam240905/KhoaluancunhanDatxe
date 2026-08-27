# Tuần 3 — Giá, cọc, kiểm xe, phí phát sinh

Trạng thái source **đến PHASE 3.6.3**. Không mô tả PHASE 3.6.4 trở đi.

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
| 3.5.2 | UI thanh toán cọc | **Chỉ CustomerApp** |
| 3.6.1 | `VehicleInspection` Handover / Return | Dispatcher: nút giao/trả (không form km) |
| 3.6.2 | `FinalAmount` từ snapshot + km thực tế | Không UI thêm |
| 3.6.3 | `BookingFee`; ExtraKm audit; Late/Fuel/Damage chưa có đơn giá | Không UI phí |

Không làm trong tuần này: OTP/email, EF Core Migration, reset `carrental.db`, cổng thanh toán, mark-paid, Balance/Refund, CRUD phí công khai, form nhập km/nhiên liệu trên UI.

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

**Chưa có:** mark-paid, webhook, cổng MoMo/VNPay thật, Admin/Dispatcher xem payment, Refund/Balance.

Seed: đơn #2 có payment BankTransfer Paid 2.240.000, `PaymentType` null (seed cũ). Không backfill.

SQLite: `EnsureSqlitePaymentTypeColumn`.

## 8. PHASE 3.5.2 — Payment UI (CustomerApp)

`booking_detail_screen.dart`: load `GET .../payments`; nếu chưa có Deposit Pending/Paid và có `quotedDepositAmount` → form chọn method, tùy chọn mã giao dịch, `POST /api/payments` (không gửi amount). Hiển thị status Pending/Paid/Failed/Refunded.

Không có UI payment trên PortalWeb / CustomerWeb / AdminWeb / DispatcherWeb / DriverApp.

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

## 12. API Backend (toàn bộ, đến 3.6.3)

Auth JWT. Role ghi trong ngoặc.

**Auth**

- `POST /api/auth/login`
- `POST /api/auth/register` (Customer)
- `GET /api/auth/me` (đã login)

**Vehicles**

- `GET /api/vehicle-types` (public)
- `GET /api/vehicles`, `GET /api/vehicles/{id}` (public)
- `POST/PUT/DELETE /api/vehicles` (Admin)

**Bookings**

- `GET /api/bookings/quote` (Customer)
- `GET /api/bookings`, `GET /api/bookings/{id}` (Customer: đơn mình; Admin/Dispatcher: tất cả)
- `POST /api/bookings` (Customer)
- `POST /api/bookings/{id}/reviews` (Customer; Completed + có TripAssignment)
- `PATCH /api/bookings/{id}/status` (Admin, Dispatcher)

**Payments**

- `POST /api/payments` (Customer, Deposit)
- `GET /api/bookings/{id}/payments` (Customer, chủ đơn)

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

Không có: endpoint inspection, endpoint fee, mark-paid, gateway.

## 13. Schema (SQLite mặc định + script SQL Server)

Bảng: Roles, Users, Customers, Drivers, VehicleTypes, Vehicles, Bookings, TripAssignments, BookingStatusHistory, Payments, Reviews, **VehicleInspections**, **BookingFees**.

Không có: RefreshToken, DriverVehicle, EF Migration.

Startup (`Program.cs`): `EnsureCreated` → ALTER/CREATE idempotent (RentalMode, snapshot, VehicleType pricing, PaymentType, VehicleInspections, FinalAmount, BookingFees) → `DbSeeder.Seed` (chỉ khi chưa có Roles) → `FillVehicleTypePricingDefaults` → `ReconcileOpenAssignmentResourceStatus`.

## 14. UI theo thành phần (đến 3.6.3)

**CustomerApp:** chọn mode trên home; quote + đặt xe; danh sách/chi tiết đơn; **cọc** trên chi tiết; đánh giá WithDriver Completed; RoleRouter Customer/Driver; tài xế complete **không** gửi km.

**DriverApp:** shell Chuyến / Trạng thái / Tài khoản; chi tiết chuyến; accept/start/complete **không** body.

**PortalWeb (:5180):** login/register chung; Customer đặt xe có mode (không quote UI, không cọc); Dispatcher xác nhận / gán (self-drive không tài xế) / giao xe / hoàn thành trả xe (nút, không form km); Admin CRUD xe, hủy đơn. Cookie `CarRentalPortalAuth`. Không trang đánh giá.

**DispatcherWeb (:5206):** **có** `Pages/Account/Login` (chỉ role Dispatcher). Chưa login → `/Account/Login` (không redirect PortalWeb). Cookie `CarRentalDispatcherAuth`. Cùng chức năng điều phối: confirm, assign, handover, complete SelfDrive.

**AdminWeb (:5258):** không login riêng; chưa login → PortalWeb. Cookie `CarRentalAdminAuth` (không chia sẻ với Portal).

**CustomerWeb (:5162):** login → PortalWeb; Register local cookie `CarRentalAuth`; đặt xe có mode; đánh giá Completed (API vẫn cần TripAssignment — SelfDrive sẽ lỗi).

## 15. Tests

`Backend.Tests` (xunit, net10.0):

- `PricingFinalAmountTests`
- `PricingFeeBreakdownTests` (ExtraKm không cộng hai lần; thiếu snapshot; late/fuel không bịa giá; type phí)
- `VehicleInspectionRulesTests`

Chạy: `dotnet test Backend.Tests/Backend.Tests.csproj -c Release` (cần Backend không lock file output nếu đang `dotnet run` cùng project).

## 16. Chưa làm (sau 3.6.3)

- PHASE 3.6.4 trở đi / 3.7
- Mark-paid, cổng MoMo/VNPay, Balance, Refund
- Đơn giá LateFee / Fuel / Damage trên `VehicleType`
- UI phí, UI nhập km/nhiên liệu, UI payment ngoài CustomerApp
- API inspection/fee công khai
- Backfill snapshot / FinalAmount / fees cho đơn cũ
- Quote UI trên PortalWeb / CustomerWeb

## 17. Tài khoản demo

Mật khẩu `Password123!`: `admin@carrental.vn`, `dispatcher@carrental.vn`, `customer1@gmail.com`, `customer2@gmail.com`, `driver1@carrental.vn`, `driver2@carrental.vn`, `driver3@carrental.vn`.

API: `http://localhost:5199`. Seed chỉ chạy trên DB trống (chưa có Roles).
