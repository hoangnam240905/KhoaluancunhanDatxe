# Tuần 4 — Đề cương 07/09/2026: điều phối, Python recommender, realtime, hợp đồng, dashboard

File này **bổ sung** sau `Tuan 3.md`. Không thay thế lịch sử Tuần 3.

- `Tuan 3.md` = snapshot source **đến PHASE 4 Group 1–3** (giá/cọc/kiểm xe, CRUD catalog, overlap Assign tối thiểu, gợi ý **C# in-process**, alert bảo trì đọc-only).
- File này = phần đã **triển khai thành source** từ đề cương mới ngày **07/09/2026** (PHASE A → F2 trên cùng codebase), **cộng** các hạng mục hoàn thiện sau đó (GAP, P0, hardening, DemoRich). Không giả mạo rằng Group 1–3 hay toàn bộ P0 thuộc đúng lịch Tuần 4.

Source cuối: **code freeze**. Hướng dẫn chạy: `HUONG_DAN_CHAY_PROJECT.txt`. Tổng quan: `README.md`.

Không EF Migration. Không reset `carrental.db` trong tài liệu này.

---

## 1. Phạm vi (sau Tuần 3)

Đề cương 07/09/2026 siết: T_buffer 2h, lock giữ chỗ, Python FastAPI, tự khóa lịch bảo trì, SignalR, hợp đồng điện tử, cọc mô phỏng Paid/Failed, dashboard.

Trong giai đoạn cập nhật sau Tuần 3, hệ thống tiếp tục được hoàn thiện với các module dưới đây (đã kiểm thử trên source cuối — không khẳng định mọi dòng đều đóng trong đúng một tuần lịch):

| Phase | Nội dung đã có trong source | UI |
|-------|-----------------------------|----|
| A | Buffer 2h; alternatives lúc Assign; `BEGIN IMMEDIATE` Deposit+Assign; hold xe lúc cọc | Dispatcher/Portal Assign alternatives |
| B | `Recommender/` FastAPI; Backend gửi snapshot REST; hard filter + weighted scoring | Home gợi ý (đã có từ Group 3, **đổi nguồn chấm điểm**) |
| C | Alert 5000 km / 180 ngày **+ chặn lịch mới** (hold/assign/recommend/alternative) | Badge + Maintenance (Group 3); hành vi khóa mới |
| D | SignalR `/hubs/realtime`, JWT groups, event sau DB success | `wwwroot/js/realtime.js`; CustomerApp/DriverApp native |
| E | Contract Issued/Signed/Voided; simulate-success/failure | Portal/CustomerWeb/CustomerApp Details; Admin list |
| F1 | Return: km, fuel %, ngoại thất, kỹ thuật; `IncidentReports`; CurrentKm monotonic; Accept trước Start | DriverApp form; AdminWeb/DispatcherWeb Incidents/Inspections |
| F2 | `GET /api/admin/dashboard` đọc-only | Portal `/Admin`, AdminWeb `/` |

Không làm (phạm vi / tùy chọn — source cuối vẫn không có): Redis, FCM, ML, GPS, gateway thật, chữ ký số, utilization theo giờ, CTR, login riêng AdminWeb, Customer self-cancel, Balance/Refund runtime.

---

## 2. Phân tích đã thành source (không bịa)

Nền Tuần 3 + Group 1–3 **giữ nguyên**, rồi nâng:

- Auth/RBAC 4 role, đổi mật khẩu (Group 1) — vẫn dùng.
- CRUD xe / loại xe / tài xế; khách = **CRUD + khóa + vô hiệu hóa** (bổ sung sau Group 1 list+lock).
- Booking / Quote / Payment Deposit Pending (3.5) — **bổ sung** hold + simulate Paid/Failed + chặn cọc đơn Cancelled.
- Dispatch overlap Group 2 — **bổ sung** buffer, alternatives, lock.
- Recommendation Group 3 C# — **chuyển chấm điểm sang Python**; C# chỉ dựng snapshot + gọi HTTP.
- MaintenanceRecord + alert Group 3 — **bổ sung** auto-block lịch mới.
- Review WithDriver — **bổ sung** rule 1–3 sao bắt buộc comment ≤500; Portal có trang Review.
- Inspection Handover/Return — **bổ sung** exterior/technical + incident.
- Hồ sơ xe — **bổ sung** giấy tờ (số ĐK + hạn) và Year 1990–2100.

---

## 3. Nghiệp vụ phức tạp

### 3.A Điều phối / xung đột (PHASE A)

`ScheduleConflictService` + `ScheduleBuffers.TechnicalHours = 2`.

Overlap sau khi nới mỗi phía **2 giờ**. Gap đúng 2 giờ **không** conflict.

Chiếm lịch: Confirmed | Assigned | InProgress, hoặc Pending đang hold + deposit Pending|Paid. Xe: `AssignedVehicleId` **hoặc** `TripAssignment.VehicleId`.

Assign conflict: không ghi gán; trả alternatives cùng `TypeId`, không maintenance-blocked, lịch trống, tài `IsActive` + Available.

Concurrency: `SqliteWriteLock` — SQLite `BEGIN IMMEDIATE` + `busy_timeout 8000`; SQL Server Serializable. Dùng lúc **tạo cọc (hold)**, **Assign**, và **đổi status (Confirm/Cancel)**. Không Redis. Mục tiêu: không double-booking; không Cancelled + hold/cọc mới.

### 3.B Recommendation (PHASE B)

```
Khách nhập start/end (+ seats/priceMax)
  → ASP.NET đọc DB, dựng snapshot, gọi POST http://127.0.0.1:8001/recommend
  → Python hard filter + score
  → UI chọn loại → POST /api/bookings?fromRecommendation=true
```

Python: `Recommender/app.py`, `scoring.py`. **Không** SQLAlchemy/SQLite. Down → Backend 503 `"Dịch vụ gợi ý xe tạm thời không khả dụng."`

Score (không ML):

`0.5 * avgRating + 0.3 * bookingCountNormalized + 0.2 * 1` (nếu còn xe usable)

Loại: type inactive; seats; priceMax; không xe usable (Status Inactive/Maintenance/Rented, lịch bận, `is_maintenance_blocked`). History completed type ids chỉ thêm reason `customerHistory`.

### 3.C Maintenance (PHASE C)

Ngưỡng: `CurrentKm` so mốc completed ≥ **5000** **hoặc** số ngày ≥ **180**, hoặc chưa có record completed.

`MaintenanceAlertService.IsBlockedForNewSchedule` dùng ở hold/assign/recommend. **Không** đổi `Vehicle.Status`. **Không** hủy trip đang InProgress/Assigned.

### 3.D Realtime (PHASE D)

Hub `[Authorize]` `/hubs/realtime`. Group: `customer:{userId}`, `driver:{userId}`, `dispatcher`, `admin`.

Event (sau DB success): BookingStatusChanged, AssignmentChanged, TripStatusChanged, VehicleStatusChanged, DriverStatusChanged, PaymentStatusChanged, ContractStatusChanged, IncidentReported (`ForDriver`, không đẩy customer).

Client: negotiate Bearer JWT → WebSocket `access_token`. `ReceiveEvent` → reload REST. Reconnect tối đa 8 lần. Flutter `kIsWeb` **return** (không WS). Không benchmark ≤1.5s.

---

## 4. Hợp đồng / thanh toán (PHASE E)

`Contracts`: Issued lúc `POST /api/bookings/{id}/contract`; Signed lúc `POST /api/contracts/{id}/simulate-sign`; Voided khi hủy booking. Snapshot tên khách, loại xe, địa chỉ, ngày, TotalAmount, DepositAmount. **Không** PKI/OTP.

Payment: tạo Deposit **Pending** (amount = QuotedDepositAmount) + hold. Booking **Cancelled** → không tạo Payment/hold. `POST .../simulate-success` → Paid + `PaidAt`. `simulate-failure` → Failed + nhả hold. Balance/Refund vẫn 400. Không MoMo/VNPay thật.

---

## 5. Driver operational (PHASE F1)

Complete WithDriver (`POST .../complete`) body optional: `odometerKm`, `fuelLevel` 0–100, `exteriorCondition`, `technicalCondition`, `notes`. Tạo Return inspection. `ActualKm` = return − handover (không cột DB). `CurrentKm` chỉ tăng nếu odo ≥ current và ≥ handover.

Start **chỉ** khi assignment `Accepted` (phải Accept trước). CustomerApp/DriverApp nút theo `assignment.status`.

Incident: type Accident | VehicleIssue | CustomerIssue | Other; Status Open; chỉ assignment của JWT. Admin/Dispatcher GET list. DriverApp form complete + báo sự cố. CustomerApp DriverTrips **vẫn complete không body**.

---

## 6. Dashboard (PHASE F2)

`DashboardService` **không ghi** DB. Label trong DTO: *"Dashboard hiện đo tiền cọc mô phỏng đã Paid, không phải tổng doanh thu thuê xe."*

| Metric trên UI | Bản chất |
|----------------|----------|
| Cọc Paid / Pending / Failed | True trên Payment; Paid = **proxy cọc**, không doanh thu thuê |
| Số đơn / assignment | True count theo CreatedAt / CompletedAt / AssignedAt |
| Snapshot xe/tài | Hiện trạng Status, không utilization-by-hour |
| Top vehicles | Count completed theo VehicleId |
| Driver runtime | Assignment completed/cancelled, incidents, AVG Review |
| Maintenance alerts | Cùng `GetAlertsAsync()`, không lọc ngày |
| Recommendation | Attribution `SourceRecommended` ≠ conversion |

Khoảng mặc định 30 ngày UTC. Query `from`/`to` tùy chọn.

---

## 7. Kiểm thử

Không cộng tay số từng file. **Lần chạy đầy đủ gần nhất đã xác nhận (code freeze): 361 passed / 0 failed / 0 skipped** (`dotnet test Backend.Tests -c Release`).

| Phase | File test chính |
|-------|-----------------|
| Nền 3 + Group 1–3 | `Pricing*`, `VehicleInspection*`, `Admin*`, `ScheduleConflictServiceTests`, `RecommendationServiceTests`, `MaintenanceAlertServiceTests`, `Phase4Group3ApiTests` |
| A | `PhaseADispatchTests`, `PhaseAVehicleHoldTests` (+ conflict tests cập nhật buffer) |
| B | `PhaseBRecommenderTests`; `Recommender/tests/test_recommend.py` |
| C | `PhaseCMaintenanceLockTests` |
| D | `PhaseDRealtimeTests` |
| E | `PhaseEContractPaymentTests` |
| F1 | `PhaseF1DriverOpsTests` |
| F2 | `PhaseF2DashboardTests` |
| Review | `ReviewRulesTests`, `PhaseGap3PortalReviewTests` |
| Customer CRUD / giấy tờ xe | `PhaseCustomerCrudTests`, `PhaseGap2VehicleDocumentTests` |
| P0 / hardening | `PhaseP0*`, `PhaseFinal*` |
| DemoRich | `PhaseDemoRichDataTests` |

Isolated SQLite; không đụng `carrental.db`. Không coverage %.

---

## 8. Schema helper thêm sau Group 3

`EnsureSqliteContractsTable`, `EnsureSqliteIncidentReportsTable`, `EnsureSqliteInspectionConditionColumns` (Exterior/Technical), `EnsureSqliteVehicleLegalColumns`, `EnsureSqliteLicensePlateUniqueIndex`. Gọi sau `EnsureCreated`, idempotent.

---

## 9. API mới (so với Tuan 3 mục 23)

- `POST /api/payments/{id}/simulate-success|simulate-failure`
- `POST/GET /api/bookings/{id}/contract`, `POST /api/contracts/{id}/simulate-sign`, `GET /api/admin/contracts`
- `GET /api/admin/dashboard`
- `GET /api/drivers/me/incidents`, `POST /api/drivers/trips/{id}/incidents`
- `GET /api/admin/incidents`, `GET /api/admin/inspections`
- `GET /api/dispatch/incidents`, `GET /api/dispatch/inspections`
- Hub `/hubs/realtime`
- Python `POST /recommend`

`GET /api/vehicle-types/recommended` **vẫn** path cũ; implementation gọi Python thay vì score C#.

---

## 10. Việc không làm (phạm vi / limitation — không ghi là thiếu bắt buộc)

OTP/email, EF Migration, reset DB, MoMo/VNPay, Refund/Balance runtime, Redis, FCM, GPS, chữ ký số, ML, utilization giờ, CTR, AdminWeb login, đổi score weights, Customer self-cancel.

Các mục sau **đã có** trên source cuối: giấy tờ xe (metadata + Year), Portal Review page, Customer Admin CRUD.

Không viết “đã hoàn thành 100% mọi yêu cầu đề cương”.

---

## 11. Thiết kế (đề cương 07/09/2026 — đã thành source)

Các module dưới đây đã được hoàn thiện thiết kế/triển khai trên codebase freeze (file UC/lớp riêng: chưa có trong repo; hành vi lấy từ README + source):

- Kiến trúc: Web/App → ASP.NET Core REST → SQLite; Recommendation qua Python FastAPI (không đọc DB).
- Realtime: SignalR `/hubs/realtime`, JWT groups.
- Module điều phối (buffer 2h, alternatives, concurrency lock).
- Module bảo trì (5000 km / 180 ngày, block lịch mới).
- Module hợp đồng / cọc mô phỏng.
- Module tài xế (Accept/Start/Complete, inspection, incident).
- Module dashboard (cọc Paid ≠ doanh thu thuê).
- Mô hình dữ liệu: Contracts, IncidentReports, cột giấy tờ xe, SourceRecommended, snapshot giá — helper SQLite, không EF Migration.

---

## 12. P0 hardening và final business rules (sau A–F2)

Không gán các mục này vào “chỉ Tuần 4”. Đây là hardening đã hoàn thiện trên source cuối:

- Booking state machine; PATCH không nhảy trạng thái (chỉ Cancel Pending/Confirmed).
- Xe hold / open assignment không xóa / không Inactive.
- LicensePlate unique, không phân biệt hoa thường.
- Driver Accept trước Start.
- Driver chỉ GET booking của assignment mình.
- Vehicle.Year 1990–2100 (Create + Update).
- Không tạo Deposit cho booking Cancelled.
- DemoRich: mỗi Driver tối đa một open assignment; DB `carrental.demo.db`.

---

## 13. Review (source cuối)

- Portal: `/Customer/Bookings/Review/{id}` (và CustomerWeb, CustomerApp).
- Backend: Completed + TripAssignment; 1–5 sao; 1–3 sao bắt buộc comment; comment ≤ 500; một review/booking.
- SelfDrive không review theo rule hiện tại.

---

## 14. Dataset demo / testing

- Core `DbSeeder` **giữ nguyên** cho tests isolated.
- DemoRich seed riêng trên `carrental.demo.db` (`dotnet run --launch-profile Demo`).
- Có Bus 29 chỗ; dữ liệu phong phú (booking, review, contract, maintenance, incident, inspection) phục vụ demo.
- Không reset `carrental.db` live.

---

## 15. Tài liệu liên quan

- Đặc tả Tuần 3 (flow giá/cọc/inspection, page catalog): `Tuan 3.md` — **giữ** làm lịch sử; một số câu Group 3 (gợi ý C#, alert không khóa lịch, chưa HĐ/dashboard, chưa giấy tờ, chưa Portal Review) **đã bị source sau đó vượt**. Khi viết báo cáo, lấy hành vi hiện tại từ file này + `README.md`.
- UC nghiệp vụ / UC hệ thống / Chương 3–4 / sequence: **chưa có file trong repo**.
