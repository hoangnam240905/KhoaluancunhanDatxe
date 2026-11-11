# Dữ liệu kiểm thử — DriveX (CNTT-KLCN108)

## 1. Kết quả kiểm thử tự động (chạy ngày 11/11/2026, mã nguồn nhánh `main`)

Các file kết quả (`KetQuaKiemThu_*.html/.txt`) nằm trong gói nộp bài, thư mục `4_KiemThu/`; CSDL demo ở `3_Database/` của gói.

| Phần | Công cụ | Số test | Kết quả | File kết quả |
|---|---|---|---|---|
| Backend (API, nghiệp vụ, CSDL) | xUnit, `dotnet test` | 929 | **929 đạt, 0 lỗi** | `KetQuaKiemThu_Backend.html` |
| App khách hàng | `flutter test` | 28 | **28 đạt** | `KetQuaKiemThu_CustomerApp.txt` |
| App tài xế | `flutter test` | 31 | **31 đạt** | `KetQuaKiemThu_DriverApp.txt` |

Ghi chú: một số test cần dịch vụ Python đang chạy (gọi thật FastAPI) được loại khỏi lần chạy tự động trên; chạy chúng khi đã bật `Chay_GoiY_Python.bat`.

Chạy lại:
```
dotnet test Backend.Tests
cd CustomerApp && flutter test
cd DriverApp && flutter test
```
Mỗi test Backend tự tạo một CSDL SQLite riêng có dữ liệu mẫu (không đụng CSDL thật), nên chạy lặp lại được.

## 2. Test theo yêu cầu của đề cương

| Yêu cầu (đề cương mục 6, 8) | Nhóm test tiêu biểu (số test) |
|---|---|
| Kiểm tra giao thoa lịch có khoảng đệm 2 giờ (Interval Overlap, T_buffer = 2h) | ScheduleConflictServiceTests (27), PhaseOccupancyConfirmTests (34), PhaseVehicleBusyPeriodsTests (20), LateReturnScheduleIntegrationTests (8), PhaseAvailabilityBookingTests (7) |
| Chống đặt trùng khi nhiều người thao tác (khóa ghi SQLite `BEGIN IMMEDIATE`) | PhaseAVehicleHoldTests (9), PhaseDriverSelectionTests (11), PhaseP061ConcreteVehicleTests (10) |
| Vòng đời đơn thuê (tiếp nhận → xác nhận → phân công → đang thuê → hoàn tất / hủy) | PhaseP0BookingStateMachineTests (56), PhaseCancelAssignedContractTermsTests (22) |
| Hợp đồng điện tử, đặt cọc giả lập, quyết toán | PhaseEContractPaymentTests (9), PhaseDispatchContractDepositGateTests (11), PhaseCustomerContractDepositGateTests (5), PricingQuoteDepositTests (6), PhaseSettlementTests (6), InvoiceTests (8) |
| Đề xuất xe / tài xế thay thế khi có sự cố; đổi xe có phụ lục | PhaseIVehicleChangeTests (6), PhaseDispatcherIncidentTests (6), PhaseDriverSelectionTests (11) |
| Cảnh báo bảo trì theo km / 6 tháng, tạm khóa nhận lịch | MaintenanceAlertServiceTests (15), PhaseCMaintenanceLockTests (12), PhaseMaintenanceCoreFlowTests (12) |
| Gợi ý phương tiện (lọc ràng buộc cứng + xếp hạng đa tiêu chí, FastAPI) | RecommendationMappingTests (15), PhaseRecommendationStage2/3Tests (16), RecommendationServiceTests (8), PhaseBRecommenderTests (5), PhaseTravelProfileStep1–4Tests (30) |
| Xác thực JWT, phân quyền RBAC, OTP, đổi mật khẩu | PhaseAuthOtpGoogleTests (24), AdminApiAuthTests (5), CustomerAccountStatusTests (15), OtpDeliveryStatusTests (4) |
| Quản lý danh mục: xe, loại xe, tài xế, khách hàng, giấy tờ lưu hành, giá | PhaseP0LicensePlateTests (20), PhaseGap2VehicleDocumentTests (13), PhaseCustomerCrudTests (8), AdminPricingPaymentTests (12), AdminCsvTests (10), PhaseP0VehicleDeleteProtectionTests (9) |
| Tài xế: nhận / bắt đầu / hoàn thành chuyến, ghi km, nhiên liệu, tình trạng xe; kiểm tra xe 2 phía | PhaseP0DriverStartTests (11), PhaseHandoverReturnInspectionTests (9), PhaseATwoSidedInspectionTests (10), VehicleInspectionRulesTests (8), PhaseF1DriverOpsTests (7) |
| Xác minh bằng lái tài xế / khách tự lái, ảnh giấy tờ | PhaseDDriverVerificationTests (16), PhaseECustomerLicenseTests (7), PhaseFUserDocumentTests (4), AdminCustomerLicenseApiTests (1) |
| Thông báo, đồng bộ realtime (SignalR) | NotificationInboxTests (16), PhaseDRealtimeTests (8) |
| Dashboard (doanh thu, số chuyến, xe thuê nhiều, hiệu suất tài xế) | PhaseF2DashboardTests (12), PhaseAdminRevenueTests (8) |
| Đánh giá sau chuyến (có tài xế và tự lái) | PhaseGap3PortalReviewTests (25), ReviewRulesTests (14) |
| Tìm kiếm / lọc xe đa tiêu chí, xe yêu thích, chatbot | PhaseP012VehicleSearchTests (18), FavoriteVehicleTests (4), ChatbotTests (28), ChatbotExtendedTests (48) |

## 3. Dữ liệu dùng để kiểm thử thủ công (UAT)

CSDL `3_Database/carrental.db` có sẵn: 47 xe thuộc các loại 4 chỗ / 7 chỗ / 9 chỗ / 16 chỗ, đơn thuê ở nhiều trạng thái (đã xác nhận, đã phân công, hoàn thành, đã hủy), biên bản giao / trả xe, phiếu bảo trì. Mật khẩu chung `Password123!` — danh sách tài khoản trong `HUONG_DAN_NOP_BAI.md`.

| # | Kịch bản | Tài khoản | Kết quả mong đợi |
|---|---|---|---|
| 1 | Khách tìm xe 7 chỗ, đặt thuê có tài xế, ký hợp đồng, đặt cọc giả lập | customer1@gmail.com | Đơn "Chờ xác nhận" → có hợp đồng + cọc "Đã thanh toán" |
| 2 | Điều phối xác nhận đơn, phân công xe + tài xế; thử chọn xe đang bận | dispatcher@carrental.vn | Xe/tài xế trùng lịch (kể cả trong 2 giờ đệm) không được chọn, có cảnh báo |
| 3 | Hai trình duyệt cùng phân công một xe cho hai đơn trùng giờ | dispatcher@carrental.vn | Chỉ một thao tác thành công, thao tác kia báo xe không còn trống |
| 4 | Điều phối ghi biên bản trước giao; tài xế đối chiếu trên app rồi bắt đầu chuyến | dispatcher + driver2@carrental.vn | Khách thấy trạng thái "Đang thuê" ngay (realtime) |
| 5 | Xe hỏng giữa chuyến: điều phối ghi sự cố, đổi xe cùng loại | dispatcher@carrental.vn | Có phụ lục đổi xe ở chi tiết đơn (web + app), xe cũ sang bảo dưỡng |
| 6 | Tài xế hoàn thành chuyến, nhập km + nhiên liệu + tình trạng xe | driver2@carrental.vn | Số km xe cập nhật; quá ngưỡng km / 6 tháng thì xe bị cảnh báo và khóa nhận lịch mới |
| 7 | Điều phối quyết toán (thêm phí hư hỏng) | dispatcher@carrental.vn | Ghi khoản thu thêm / hoàn lại, đơn rời hàng đợi "Chờ quyết toán" |
| 8 | Khách đánh giá chuyến (có tài xế và tự lái) | customer1@gmail.com | Đánh giá hiện trong Dashboard admin |
| 9 | Khách mới đặt tự lái khi chưa khai bằng lái | khach3@drivex.demo | Bị yêu cầu khai bằng; điều phối duyệt xong mới xác nhận được |
| 10 | Admin xem Dashboard | admin@carrental.vn | Doanh thu, số chuyến, tỷ lệ sử dụng xe, xe thuê nhiều, hiệu suất tài xế, hiệu quả gợi ý, cảnh báo bảo dưỡng |
| 11 | Khách xem gợi ý xe (bật dịch vụ Python) | customer1@gmail.com | Danh sách xe xếp hạng, không có xe bận / đang bảo trì |
