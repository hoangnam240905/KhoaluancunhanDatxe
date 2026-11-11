# Hướng dẫn sử dụng — DriveX

Hệ thống gồm: **Web** (http://localhost:5180) cho khách hàng, điều phối viên (`/DieuPhoi`) và quản trị viên (`/Admin`); **App khách hàng** và **App tài xế** (Android). Tất cả dùng chung một Backend API và một CSDL. Đăng nhập bằng tài khoản demo (mật khẩu `Password123!`, danh sách trong `HUONG_DAN_NOP_BAI.md`); sau khi đăng nhập web tự chuyển tới trang của đúng vai trò.

---

## 1. Khách hàng (web hoặc App khách hàng)

**Tài khoản**
- *Đăng ký*: nhập họ tên, email, số điện thoại, mật khẩu → nhập mã OTP gửi qua email. *Quên mật khẩu*: nhận OTP qua email để đặt mật khẩu mới.
- *Hồ sơ cá nhân* (web) / *Tài khoản* (app): sửa thông tin, đổi mật khẩu, xem *Xe yêu thích*.
- *Bằng lái xe (thuê tự lái)*: khai số bằng, hạng bằng, ngày cấp, ngày hết hạn và gửi ảnh 2 mặt. Điều phối viên duyệt một lần; hạng bằng quyết định loại xe được tự lái (hạng B: xe đến 8 chỗ không tính ghế lái; xe 16 chỗ cần D1).

**Tìm và đặt xe**
1. *Xe* (web) / *Trang chủ* (app): lọc theo hình thức (tự lái / có tài xế), loại xe, số chỗ, giá, ngày nhận – trả; chỉ hiện xe còn trống lịch. Trang *Gợi ý* đưa ra danh sách xe phù hợp nhu cầu.
2. Mở chi tiết xe: thông số, ảnh, lịch bận, đánh giá; bấm ♥ để lưu vào xe yêu thích.
3. *Đặt xe*: chọn thời gian, điểm đón / trả, (có tài xế) chọn tài xế mong muốn → xem báo giá → gửi đơn. Đơn ở trạng thái *Chờ xác nhận*.
4. Khi điều phối xác nhận: mở *Đơn đặt* → chi tiết đơn → *Hợp đồng điện tử* → đọc điều khoản, tick đồng ý, ký. Sau đó *Đặt cọc* (50% tổng tiền, thanh toán giả lập: chọn phương thức → xác nhận). Có đủ hợp đồng đã ký và cọc thì điều phối mới phân công xe.

**Theo dõi đơn**
- Trạng thái đơn cập nhật tức thời (realtime): Chờ xác nhận → Đã xác nhận → Đã phân công → Đang thuê → Hoàn thành (hoặc Đã hủy). Chuông 🔔 hiện thông báo khi đơn đổi trạng thái.
- Chi tiết đơn có: thông tin xe / tài xế, biên bản giao / trả xe, phụ phí, *Phụ lục đổi xe* (nếu điều phối đổi xe), biên nhận và hóa đơn.
- *Hủy đơn*: được tự hủy khi đơn chưa phân công, hoặc đã phân công nhưng còn ít nhất 24 giờ trước giờ nhận xe; phải ghi lý do.
- Sau khi chuyến hoàn thành: *Đánh giá chuyến đi* (1–5 sao, nhận xét; 1–3 sao bắt buộc ghi nhận xét).
- Trợ lý *Hỏi DriveX* (chatbot) trả lời câu hỏi về giá, quy trình, xe còn trống.

---

## 2. Điều phối viên (web, `/DieuPhoi`)

| Menu | Việc làm |
|---|---|
| **Tổng quan** | Việc cần làm hôm nay, đơn mới, sự cố đang mở, thông báo. |
| **Đặt thuê & Xác nhận** | Xem đơn chờ xác nhận → *Xác nhận* (hệ thống kiểm tra còn xe trống, đơn tự lái cần bằng lái đã duyệt). Đơn đã đủ hợp đồng + cọc → *Phân công*: chỉ hiện xe và tài xế rảnh trong khung giờ (đã tính 2 giờ đệm giữa hai chuyến), tài xế đủ hạng bằng và đã xác minh. |
| **Lịch xe & tài xế** | Lịch dạng thời gian của từng xe và từng tài xế, thấy chỗ trống / trùng lịch. |
| **Vận hành chuyến** | Theo dõi mọi chuyến: <br>• *Kiểm tra trước giao* (đơn có tài xế): ghi km, nhiên liệu, ngoại thất, kỹ thuật; tài xế đối chiếu trên app, nếu sai lệch thì sửa biên bản và bấm *Đã giải quyết*. <br>• *Giao xe* (tự lái): ghi biên bản, tick đã kiểm tra bằng lái gốc. <br>• *Báo sự cố*: ghi sự cố khách / tài xế gọi báo; *Đã xử lý* kèm cách xử lý. <br>• *Đổi xe*: hệ thống đề xuất xe cùng loại còn trống; hết xe cùng loại mới hiện loại khác kèm chênh lệch giá (phải xác nhận khách đồng ý). Đổi giữa chuyến nhập km + nhiên liệu của cả 2 xe. <br>• *Trả xe*: ghi biên bản trả, hệ thống tính km thực tế, phụ phí. <br>• *Quyết toán*: nhập phí phát sinh (nhiên liệu, hư hỏng, khác) → chốt; hệ thống ghi khoản thu thêm / hoàn lại. |
| **Biên bản kiểm xe** | Danh sách biên bản trước giao / giao / trả xe; đối chiếu phiếu trả xe của tài xế (khớp / lệch → tạo sự cố). |
| **Xác minh tài xế** | Duyệt ảnh bằng lái + CCCD của tài xế (bước 1), đối chiếu bằng gốc và xác nhận (bước 2). Tài xế chưa xác minh không được phân công. |
| **Duyệt bằng lái khách** | Duyệt / từ chối (kèm lý do) bằng lái của khách thuê tự lái. |
| **Thông báo** | Toàn bộ thông báo; mới đến hiện ngay ở chuông kèm thông báo nổi. |

Mọi thao tác (biên bản, duyệt, đổi xe, quyết toán, xử lý sự cố) đều lưu tên người thực hiện và thời điểm.

---

## 3. Quản trị viên (web, `/Admin`)

| Menu | Việc làm |
|---|---|
| **Tổng quan (Dashboard)** | Chọn khoảng ngày: doanh thu, số chuyến, cọc theo tháng, xếp hạng xe được thuê nhiều, khung giờ nhận xe cao điểm, đánh giá mới, **tỷ lệ sử dụng xe, hiệu quả gợi ý, hiệu suất tài xế, cảnh báo bảo dưỡng**. |
| **Đơn thuê** | Tra cứu, xem chi tiết mọi đơn. |
| **Quản lý xe** | Thêm / sửa / ngừng xe: biển số, hãng, đời, ảnh, km hiện tại, giấy tờ lưu hành (đăng ký, đăng kiểm, bảo hiểm và hạn); quản lý loại xe và **biểu giá** (giá ngày, giá km, giá tự lái, km miễn phí, hạng bằng tối thiểu). |
| **Khách hàng** | Thêm / sửa / khóa tài khoản khách. |
| **Duyệt bằng lái khách** | Như điều phối viên. |
| **Tài xế** | Thêm / sửa tài xế, số / hạng / hạn bằng, xem ảnh giấy tờ, trạng thái hoạt động. |
| **Thanh toán & Hợp đồng** | Danh sách thanh toán, hợp đồng, hóa đơn (gửi lại hóa đơn qua email). |
| **Bảo trì & Sự cố** | Phiếu bảo trì (lên lịch, hoàn thành), xe đến hạn bảo dưỡng (vượt ngưỡng km hoặc 6 tháng — xe đến hạn tự bị khóa nhận lịch mới), danh sách sự cố. |
| **Báo cáo** | Báo cáo doanh thu / vận hành; nhập – xuất CSV khách hàng, xe, tài xế. |

---

## 4. Tài xế (App tài xế)

- **Đăng ký / đăng nhập**: tài xế mới đăng ký trên app rồi vào *Tài khoản → Bằng lái & giấy tờ* khai bằng và chụp 4 ảnh (bằng lái 2 mặt, CCCD 2 mặt). Có *Quên mật khẩu* qua OTP email.
- **Chuyến**: danh sách chuyến được phân công (cập nhật tức thời); chuông 🔔 mở lịch sử thông báo.
- **Chi tiết chuyến**:
  1. *Nhận chuyến*.
  2. Xem *biên bản trước giao* của điều phối, đối chiếu xe thật: đúng → *Bắt đầu chuyến*; sai → *Báo sai lệch* (chờ điều phối sửa). Chưa có biên bản → *Nhắc điều phối*.
  3. Trong chuyến: *Báo sự cố* (tai nạn, xe hỏng, vấn đề với khách…).
  4. *Hoàn thành chuyến*: nhập số km, mức nhiên liệu, tình trạng ngoại thất và kỹ thuật. Dữ liệu này cập nhật km của xe và kích hoạt cảnh báo bảo trì khi tới ngưỡng.
  - Nếu xe bị đổi: mục *Phụ lục đổi xe* cho biết xe mới, lý do, km lúc đổi.
- **Lịch**: chọn ngày để xem các chuyến được giao trong ngày đó.
- **Sự cố**: các sự cố đã báo và kết quả xử lý.
- **Trạng thái**: chuyển Rảnh (Available) / Nghỉ (Offline); đang có chuyến chưa xong thì không chuyển sang Rảnh được.
- **Tài khoản**: thông tin cá nhân, bằng lái & giấy tờ, đổi mật khẩu, giao diện tối, đăng xuất.
