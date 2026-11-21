# CNPM-NHOM4 - TuyenSinhOnline
# HỆ THỐNG QUẢN LÝ TUYỂN SINH TRỰC TUYẾN

> **Đồ án môn Công nghệ phần mềm — Nhóm 4**  
> **Giảng viên hướng dẫn:** TS. Cao Thanh Sơn — Trường Đại học Vinh  
> **Phiên bản hoàn thiện:** Hết Sprint 3

---

## 1. Giới thiệu tổng quan & Phạm vi các Sprint

Hệ thống **TuyenSinhOnline** là giải pháp phần mềm hiện đại hóa toàn diện công tác tuyển sinh đại học, từ khâu công bố kế hoạch chỉ tiêu đến việc nộp hồ sơ xét tuyển trực tuyến và quản lý hồ sơ của thí sinh.

Hệ thống được phát triển theo mô hình Scrum/Agile qua các Sprint:

* **Sprint 1 (Nền tảng & Cấu hình tuyển sinh):**
  - Hệ thống tài khoản và phân quyền 3 vai trò: `QuanTriVien` (Admin), `CanBoTuyenSinh` (Cán bộ), `ThiSinh` (Thí sinh).
  - Quản lý danh mục Ngành học (`/Majors`), Phương thức xét tuyển (`/AdmissionMethods`), Đợt tuyển sinh (`/AdmissionRounds`).
  - Phân bổ chỉ tiêu ngành theo đợt và cấu hình phương thức xét tuyển áp dụng (`/Quotas`).
* **Sprint 2 (Cổng thí sinh & Hồ sơ cá nhân):**
  - Cổng thông tin tra cứu ngành học, chỉ tiêu và điều kiện xét tuyển dành riêng cho thí sinh (`/Candidate`).
  - Quản lý và chuẩn hóa hồ sơ cá nhân (`CandidateProfiles`) và thông tin quá trình học tập cấp THPT (`CandidateEducations`) tại `/CandidateProfile`.
* **Sprint 3 (Đăng ký xét tuyển & Quản lý hồ sơ đã nộp):**
  - **S3-01 & S3-02:** Mô hình CSDL hồ sơ xét tuyển (`AdmissionApplications`, `ApplicationScores`) và biểu mẫu nộp hồ sơ trực tuyến (`/ApplicationForm/Create`).
  - **S3-03:** Kiểm tra điều kiện nộp hồ sơ và thời gian tiếp nhận chặt chẽ phía máy chủ (kiểm tra hồ sơ cá nhân/học vấn, kiểm tra khung giờ mở/đóng đợt, kiểm tra dây chuyền Đợt $\rightarrow$ Ngành $\rightarrow$ Phương thức và miền giá trị điểm).
  - **S3-04:** Lưu hồ sơ an toàn bằng Database Transaction, lưu **Snapshot bất biến** thông tin cá nhân và điểm xét tuyển tại thời điểm gửi, tự động sinh mã hồ sơ duy nhất dạng `HS{Năm}-{6 số ngẫu nhiên}` (kèm retry khi chạm mã trùng), gán trạng thái ban đầu **Chờ tiếp nhận**, chống gửi lặp 3 lớp (UI + Server/DB + PRG).
  - **S3-05:** Trang danh sách hồ sơ đã nộp (`/ApplicationForm`), bộ lọc theo đợt và tìm kiếm theo mã hồ sơ, trang xem chi tiết dữ liệu snapshot (`/ApplicationForm/Details/{id}`), bảo mật tuyệt đối chống truy cập trái phép **IDOR**, và cập nhật Dashboard Cổng thí sinh (`/Candidate`).
  - **S3-06:** Bộ kiểm thử tự động toàn diện (62/62 test cases unit/integration + Script E2E `scripts/Test-Sprint3.ps1`).
  - **S3-07:** Đóng gói bản phát hành Sprint 3, tài liệu và dữ liệu demo chuẩn.

---

## 2. Công nghệ sử dụng & Môi trường yêu cầu

* **Nền tảng phát triển:** .NET 9 SDK (C# 13, ASP.NET Core MVC 9.0)
* **Cơ sở dữ liệu & ORM:** Microsoft SQL Server (hỗ trợ SQL Server 2019/2022 và LocalDB), Entity Framework Core 9.0
* **Xác thực & Phân quyền:** ASP.NET Core Identity (sử dụng mật khẩu đã băm, chống tấn công Brute-force qua Rate Limiter)
* **Giao diện người dùng:** Razor Views (HTML5, Responsive CSS hiện đại không phụ thuộc framework ngoài nặng nề, Vanilla JavaScript)
* **Bảo mật:** Chống CSRF/XSRF bằng `AutoValidateAntiforgeryTokenAttribute`, bảo vệ dữ liệu chống IDOR, chống Over-posting, chống SQL Injection với EF Core Parameterization.

---

## 3. Hướng dẫn cài đặt & Khởi chạy Website

### Bước 1: Chuẩn bị Cơ sở dữ liệu
Trong tệp `src/TuyenSinhOnline/appsettings.json`, chuỗi kết nối mặc định sử dụng Windows Authentication:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.;Database=CNPM;Trusted_Connection=True;TrustServerCertificate=True"
}
```
*(Nếu sử dụng SQL Server Authentication, cấu hình dạng: `Server=localhost;Database=CNPM;User Id=sa;Password=YourPassword;TrustServerCertificate=True`)*.

### Bước 2: Chạy Website & Khởi tạo dữ liệu tự động
Dự án đã trang bị sẵn script khởi chạy tự động chuẩn bị tài khoản và dữ liệu demo:
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Start-Website.ps1
```
* Website khởi chạy tại: **`http://localhost:5080`** (hoặc `https://localhost:7080`).
* Mật khẩu demo chung được tạo ngẫu nhiên và lưu an toàn tại `.local/demo-password.txt` (tự động loại trừ khỏi Git).

### Danh sách tài khoản demo có sẵn:
| Vai trò | Email đăng nhập | Mô tả & Mục đích kiểm thử |
| :--- | :--- | :--- |
| **Quản trị viên** | `admin.demo@tuyensinh.local` | Quản lý danh mục ngành, phương thức, đợt và phân bổ chỉ tiêu (`/AdmissionRounds`, `/Quotas`). |
| **Cán bộ tuyển sinh** | `canbo.demo@tuyensinh.local` | Không gian cán bộ tuyển sinh (`/Admissions`). |
| **Thí sinh 1 (Đã đủ hồ sơ)** | `thisinh.demo@tuyensinh.local` | Đã có sẵn hồ sơ cá nhân và học vấn THPT, đã nộp 1 nguyện vọng mẫu $\rightarrow$ Dùng để demo nộp hồ sơ, xem danh sách và chi tiết. |
| **Thí sinh 2 (Chưa có hồ sơ)** | `thisinh2.demo@tuyensinh.local` | Tài khoản mới tinh chưa tạo hồ sơ cá nhân $\rightarrow$ Dùng để demo tình huống bị từ chối **TC03** và kiểm chứng **chống IDOR**. |

---

## 4. Hướng dẫn chạy các bộ kiểm thử tự động

Hệ thống sở hữu bộ kiểm thử tự động hóa 100% bao phủ toàn bộ các phân hệ từ Sprint 1 đến Sprint 3:

### 1. Kiểm thử Unit & Integration Tests (chạy không cần website bật):
```powershell
# 1. Kiểm thử quy tắc thời gian tiếp nhận & chuẩn hóa dữ liệu (13 ca)
dotnet run --project tests/TimeRules

# 2. Kiểm thử Cổng thí sinh, S3-03, S3-04, S3-05 và chống IDOR (32 ca)
dotnet run --project tests/CandidatePortal

# 3. Kiểm thử phân hệ Quản trị viên & Cán bộ tuyển sinh (12 ca)
dotnet run --project tests/StaffPortal

# 4. Kiểm thử tính toàn vẹn dữ liệu mẫu ban đầu (5 ca)
dotnet run --project tests/DemoData
```
👉 **Tổng cộng: 62/62 test cases đạt PASS 100%.**

### 2. Kiểm thử End-to-End qua giao thức HTTP (chạy khi website đang mở):
Mở một cửa sổ PowerShell riêng biệt và thực thi script kiểm thử:
```powershell
# Kiểm thử toàn diện Sprint 3 (TC01 đến TC29):
.\scripts\Test-Sprint3.ps1

# Kiểm thử chức năng Quản lý tuyển sinh Sprint 1:
$demoPassword = (Get-Content -LiteralPath .local/demo-password.txt -Raw).Trim()
.\scripts\Test-Management.ps1 -DemoPassword $demoPassword
```

---

## 5. Quy trình 10 bước Demo nghiệm thu Sprint 3

Dưới đây là kịch bản chuẩn 10 bước để trình diễn chức năng Sprint 3 cho Giảng viên hướng dẫn / Hội đồng nghiệm thu:

1. **Bước 1 — Đăng nhập Thí sinh 2:** Đăng nhập tài khoản `thisinh2.demo@tuyensinh.local` tại `/Account/Login`.
2. **Bước 2 — Demo kiểm tra điều kiện (S3-03 / TC03):** Vào mục **Đăng ký xét tuyển** (`/ApplicationForm/Create`) $\rightarrow$ Hệ thống lập tức từ chối và hiển thị cảnh báo: *"Bạn chưa hoàn thiện hồ sơ cá nhân"* kèm nút dẫn sang `/CandidateProfile/Edit`.
3. **Bước 3 — Đăng xuất và đăng nhập Thí sinh 1:** Đăng nhập bằng `thisinh.demo@tuyensinh.local` (tài khoản đã có đủ thông tin).
4. **Bước 4 — Kiểm tra hồ sơ cá nhân & Cổng thí sinh:** Vào `/CandidateProfile` xem thông tin cá nhân và trường THPT; vào `/Candidate` xem các đợt tuyển sinh đang mở.
5. **Bước 5 — Mở form nộp hồ sơ xét tuyển:** Vào `/ApplicationForm/Create`, chọn Ngành học (ví dụ: CNTT) $\rightarrow$ Dropdown phương thức tự động lọc theo ngành $\rightarrow$ Chọn phương thức $\rightarrow$ Các ô điểm xuất hiện động theo đúng quy định của phương thức.
6. **Bước 6 — Demo validate máy chủ (S3-03 / TC11):** Thử nhập điểm vượt quá giới hạn (ví dụ: nhập `35` điểm cho học bạ) và bấm **Gửi hồ sơ** $\rightarrow$ Nút submit tự khóa chống spam, server trả lại lỗi validation rõ ràng.
7. **Bước 7 — Nộp hồ sơ hợp lệ (S3-04):** Nhập điểm hợp lệ (ví dụ: `28.5`) và bấm **Gửi hồ sơ** $\rightarrow$ Hệ thống bọc transaction, lưu dữ liệu, sinh mã duy nhất dạng `HS2026-XXXXXX`, gán trạng thái **Chờ tiếp nhận**, và chuyển hướng PRG sang trang chi tiết hồ sơ `/ApplicationForm/Details/{id}`.
8. **Bước 8 — Kiểm tra Snapshot bất biến:** Vào `/CandidateProfile/Edit` sửa tên thí sinh thành tên khác $\rightarrow$ Quay lại trang `/ApplicationForm/Details/{id}`: Hồ sơ đã gửi vẫn giữ nguyên thông tin cá nhân lúc nộp ban đầu.
9. **Bước 9 — Xem danh sách hồ sơ & Cổng thí sinh (S3-05):** Vào `/ApplicationForm` xem danh sách toàn bộ hồ sơ đã nộp với badge trạng thái **Chờ tiếp nhận** nổi bật; thử tìm kiếm theo mã hồ sơ $\rightarrow$ Kết quả lọc chính xác. Quay lại `/Candidate` xác nhận số lượng hồ sơ nộp hiển thị đúng.
10. **Bước 10 — Kiểm chứng bảo mật chống IDOR:** Sao chép link chi tiết hồ sơ của Thí sinh 1 (`/ApplicationForm/Details/{id}`). Đăng xuất và đăng nhập bằng Thí sinh 2 (`thisinh2.demo@tuyensinh.local`), sau đó dán lại link trên vào thanh địa chỉ $\rightarrow$ Hệ thống trả về lỗi **404 Not Found**, tuyệt đối không để lộ dữ liệu thí sinh khác.

---

## 6. Danh mục 10 ảnh minh chứng nghiệm thu (Lưu tại `docs/screenshots/`)

Khi chạy nghiệm thu thực tế, chụp ảnh màn hình lưu vào thư mục `docs/screenshots/` theo đúng tên sau:

1. `01-dang-nhap-thisinh.png`: Màn hình đăng nhập tài khoản thí sinh (`/Account/Login`).
2. `02-ho-so-ca-nhan.png`: Màn hình quản lý hồ sơ cá nhân và học vấn THPT (`/CandidateProfile`).
3. `03-bieu-mau-dang-ky.png`: Màn hình biểu mẫu đăng ký xét tuyển với dropdown ngành & phương thức (`/ApplicationForm/Create`).
4. `04-tu-choi-vi-pham-s3-03.png`: Màn hình thông báo lỗi khi vi phạm điều kiện (chưa có hồ sơ hoặc nhập điểm sai).
5. `05-nop-thanh-cong-prg.png`: Màn hình thông báo nộp thành công kèm Mã hồ sơ duy nhất (PRG).
6. `06-danh-sach-ho-so-s3-05.png`: Màn hình danh sách hồ sơ đã nộp với badge **Chờ tiếp nhận** (`/ApplicationForm`).
7. `07-chi-tiet-ho-so-snapshot.png`: Màn hình chi tiết hồ sơ hiển thị đầy đủ thông tin snapshot và bảng điểm đã nộp (`/ApplicationForm/Details`).
8. `08-chong-idor-404.png`: Màn hình chặn truy cập trái phép 404 khi Thí sinh 2 mở link hồ sơ của Thí sinh 1.
9. `09-truy-van-sql-server.png`: Ảnh chụp cửa sổ SSMS truy vấn bảng `AdmissionApplications` và `ApplicationScores` chứng minh dữ liệu snapshot và chỉ mục `UNIQUE` trong SQL Server.
10. `10-terminal-chay-test-100-pass.png`: Ảnh chụp terminal chạy `dotnet build` và các bộ test đạt **PASS 100%**.
