# Hướng dẫn cài đặt — DriveX (CNTT-KLCN108)

Nhóm: Đỗ Trần Hoàng Nam (2001230541) — Backend + Web · Nguyễn Thành Đạt (2001230142) — App tài xế · Lâm Nguyễn Dương (2001230137) — App khách hàng.
GVHD: Phan Thị Ngọc Mai.

## Cấu trúc mã nguồn

| Thư mục | Thành phần | Công nghệ |
|---|---|---|
| `Backend/` | API (đặt xe, điều phối, hợp đồng, thanh toán, biên bản, đổi xe, quyết toán, thông báo realtime) | ASP.NET Core .NET 10, EF Core + SQLite, JWT, SignalR |
| `Backend.Tests/` | Kiểm thử tự động (~930 test) | xUnit |
| `PortalWeb/` | Web cho khách hàng, điều phối viên (`/DieuPhoi`), quản trị (`/Admin`) | ASP.NET Core Razor Pages |
| `CustomerApp/` | App khách hàng | Flutter (Dart 3.11) |
| `DriverApp/` | App tài xế | Flutter (Dart 3.11) |
| `RecommendationService/`, `Recommender/` | Dịch vụ gợi ý xe | Python (FastAPI) |

## Thư viện sử dụng

Thư viện không chép kèm; cài tự động bằng lệnh bên dưới (danh sách đầy đủ trong các file khai báo).

- **.NET** (`Backend/Backend.csproj`): Microsoft.EntityFrameworkCore.Sqlite 10.0.11, Microsoft.EntityFrameworkCore.SqlServer 10.0.11, Microsoft.AspNetCore.Authentication.JwtBearer 10.0.11, Microsoft.AspNetCore.OpenApi 10.0.3, BCrypt.Net-Next 4.2.0, Google.Apis.Auth 1.76.0. → `dotnet restore`
- **Flutter** (`CustomerApp/pubspec.yaml`, `DriverApp/pubspec.yaml`): http ^1.2.2, shared_preferences ^2.3.3, intl ^0.20.2, image_picker ^1.2.2, cupertino_icons ^1.0.8 (dev: flutter_lints, flutter_test). → `flutter pub get`
- **Python** (`RecommendationService/requirements.txt`, `Recommender/requirements.txt`): fastapi, uvicorn, pydantic, httpx, pandas, numpy, scikit-learn, datasets, pytest. → `pip install -r requirements.txt`

Phần mềm cần cài: .NET SDK 10 (hoặc ASP.NET Core Runtime 10 nếu chỉ chạy bản đóng gói), Flutter SDK (Dart ^3.11), Android SDK (để chạy app Android), Python 3.11+ (dịch vụ gợi ý, không bắt buộc).

## Chạy từ mã nguồn

```
dotnet run --project Backend/Backend.csproj        # API :5199 (tự tạo bảng + dữ liệu mẫu nếu chưa có CSDL)
dotnet run --project PortalWeb/PortalWeb.csproj    # Web :5180
cd CustomerApp && flutter pub get && flutter run   # App khách hàng
cd DriverApp && flutter pub get && flutter run     # App tài xế
dotnet test Backend.Tests                          # Kiểm thử
```

Gửi email (OTP, hóa đơn) cần cấu hình Gmail App Password trong `Email:SmtpPassword` (không kèm trong bài nộp). Khi chưa cấu hình, hóa đơn vẫn xem được trên web; chạy thử có thể tắt gửi mail bằng `--Email:Enabled=false`.

## Tài khoản demo (mật khẩu chung: `Password123!`)

| Vai trò | Email |
|---|---|
| Quản trị | admin@carrental.vn |
| Điều phối viên | dispatcher@carrental.vn |
| Khách hàng (đã duyệt bằng lái B) | customer1@gmail.com, customer2@gmail.com |
| Khách hàng (chưa khai bằng lái — demo luồng khai + duyệt bằng) | khach3@drivex.demo, khach4@drivex.demo |
| Tài xế (đã xác minh) | driver1@carrental.vn, driver2@carrental.vn, driver3@carrental.vn (bằng B2), taixe4@drivex.demo (bằng D1) |
| Tài xế (chờ xác minh — demo luồng xác minh) | taixe5@drivex.demo, taixe6@drivex.demo |
