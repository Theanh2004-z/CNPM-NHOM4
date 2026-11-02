# CNPM-NHOM4 - TuyenSinhOnline

Website cho đề tài **Xây dựng hệ thống quản lý tuyển sinh trực tuyến** - Nhóm 4 môn Công nghệ phần mềm.

## Công nghệ sử dụng
- **Backend**: ASP.NET Core MVC (.NET 9)
- **Database & ORM**: SQL Server, Entity Framework Core 9
- **Authentication & Authorization**: ASP.NET Core Identity
- **Giao diện hiện tại**: MVC Razor Views, CSS riêng và jQuery Validation đi kèm dự án.

## Chạy bản Sprint 1 tích hợp

1. Khởi động dịch vụ **SQL Server (MSSQLSERVER)**. Cấu hình hiện tại kết nối `Server=.;Database=CNPM` bằng Windows Authentication.
2. Mở `TuyenSinhOnline.sln`, đặt `TuyenSinhOnline` làm Startup Project, chọn profile **http** và nhấn **Ctrl+F5**.
3. Website chạy tại `http://localhost:5080`. Profile HTTPS giữ nguyên `https://localhost:7080`.
4. Để chuẩn bị tài khoản demo và chạy website, dùng `powershell -ExecutionPolicy Bypass -File .\scripts\Start-Website.ps1`. Script sử dụng SQL Server trong cấu hình, không yêu cầu LocalDB. Mật khẩu được giữ riêng ở `.local/demo-password.txt`; tài khoản đã tồn tại không bị đổi mật khẩu.

Các tài khoản demo: `admin.demo@tuyensinh.local` (quản trị), `canbo.demo@tuyensinh.local` (cán bộ), `thisinh.demo@tuyensinh.local` (thí sinh). Tài khoản tự đăng ký luôn là thí sinh. Muốn xem các màn hình quản lý mới, đăng nhập bằng tài khoản **quản trị**.


## Phần Hoàng và Thảo đã tích hợp

- `/Majors`: tìm kiếm, thêm, sửa, ẩn và sử dụng lại ngành; kiểm tra mã duy nhất.
- `/AdmissionMethods`: quản lý phương thức, điều kiện xét tuyển và trạng thái sử dụng.
- `/AdmissionRounds`: quản lý mã đợt, tên đợt, năm và thời gian tuyển sinh; trang Cấu hình dẫn tới ngành/chỉ tiêu/phương thức của đợt.
- `/Quotas`: cấu hình chỉ tiêu riêng cho mỗi cặp ngành–đợt, chọn các phương thức áp dụng; sửa, bật/tắt và giữ lịch sử liên kết.

Các màn hình trên chỉ dành cho `QuanTriVien`; cả yêu cầu đọc và ghi đều kiểm tra quyền ở máy chủ. Thời gian nhập/hiển thị là **giờ Việt Nam (UTC+7)**, mở tại StartAt và đóng tại EndAt. Ngành/phương thức/đợt ngừng sử dụng không được chọn cho cấu hình mới.

Không cần tạo migration mới cho đợt tích hợp này. Mã nguồn dùng các bảng hiện có trong CNPM; không xóa hoặc tạo lại database. Dịch vụ SQL Server phải được khởi động trước khi chạy.

## Kiểm thử và demo

Chi tiết phân công, kiểm thử và kịch bản demo: [docs/SPRINT1-HOANG-THAO.md](docs/SPRINT1-HOANG-THAO.md).

- `scripts/Test-Management.ps1`: 29 ca HTTP thực, chạy bằng PowerShell 7 trên website gắn **database thử riêng**. Script thêm và chỉnh sửa dữ liệu thử, không tự xóa sau khi chạy.
- `dotnet run --project tests/TimeRules`: 8 kiểm tra mốc mở/đóng và chuẩn hóa đầu vào.
- `scripts/Test-Sprint1.ps1`: bộ kiểm thử tài khoản trước đó; dùng database thử vì script tạo và khóa tài khoản thử, đồng thời cố ý chạm giới hạn yêu cầu đăng nhập.

Ví dụ chạy kiểm thử quản lý sau khi khởi động website trên database thử:

```powershell
$demoPassword = (Get-Content -LiteralPath .local/demo-password.txt -Raw).Trim()
.\scripts\Test-Management.ps1 -DemoPassword $demoPassword
```

Các phần hồ sơ cá nhân, đăng ký xét tuyển, xử lý hồ sơ và thống kê thuộc các Sprint sau; chưa được triển khai trong lần này.
