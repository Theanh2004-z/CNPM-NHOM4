# Bàn giao chức năng Sprint 1 của Hoàng và Thảo

Ngày cập nhật: 02/11/2026. Bản tích hợp triển khai phần việc trong kế hoạch nhóm, dùng chung đăng nhập/phân quyền của Thế Anh và các bảng SQL Server CNPM đã có. Đây là mã nguồn được bổ sung trong phiên hỗ trợ này; chưa phải biên bản nghiệm thu chéo của các thành viên.

## Phần theo phân công của Phạm Ngọc Hoàng

- S1-02: đối chiếu cấu trúc SQL Server với các lớp dữ liệu và migration sẵn có. Không tạo lại CNPM hoặc xóa dữ liệu hiện có.
- S1-05: danh sách, tìm kiếm, lọc, thêm/sửa/ẩn/sử dụng lại ngành; mã và tên bắt buộc, mã duy nhất, không trùng chính bản ghi khi sửa.
- S1-06: chỉ tiêu số nguyên không âm theo cặp ngành–đợt duy nhất, đọc lại dữ liệu sau khi lưu; giữ các tham chiếu lịch sử.
- S1-09: ghép controller, view và kiểm tra quyền vào dự án chung; bổ sung bộ kiểm thử tích hợp cho các chức năng mới.

Các tệp chính: `Controllers/MajorsController.cs`, `Controllers/QuotasController.cs`, `Models/Management/ManagementModels.cs`, `Views/Majors`, `Views/Quotas`.

## Phần theo phân công của Nguyễn Thị Thảo

- S1-07: thêm/sửa/tìm/ngừng/sử dụng lại phương thức; mã duy nhất, tên và điều kiện được lưu lại.
- S1-08: thêm/sửa đợt, năm tuyển sinh và thời điểm mở/đóng; xem trạng thái theo thời gian; cấu hình phương thức theo từng ngành trong đợt qua RoundMajorMethods.
- S1-10: kịch bản demo, ảnh giao diện và hướng dẫn chạy/kiểm thử trong tài liệu này. Các thành viên vẫn cần tự kiểm tra, giải thích và trình bày phần được phân công.

Các tệp chính: `Controllers/AdmissionMethodsController.cs`, `Controllers/AdmissionRoundsController.cs`, `Views/AdmissionMethods`, `Views/AdmissionRounds`. Các đường dẫn mã nguồn trong tài liệu tính từ `src/TuyenSinhOnline`.

## Quy tắc đã triển khai

1. Cả bốn controller quản lý kế thừa `ManagementControllerBase`, yêu cầu vai trò QuanTriVien và kiểm tra token chống giả mạo cho POST.
2. Mã được trim và đổi sang chữ hoa; tên chỉ có khoảng trắng bị từ chối. Kiểm tra trùng trước khi lưu và bắt xung đột ràng buộc duy nhất từ SQL Server.
3. Chỉ tiêu là số nguyên >= 0. Một ngành trong một đợt chỉ có một cấu hình. Bật cấu hình cần ít nhất một phương thức.
4. Khi sửa cấu hình, không đổi cặp ngành–đợt để bảo toàn lịch sử. Muốn dùng cặp khác phải tạo cấu hình mới.
5. Bỏ chọn phương thức sẽ đặt IsEnabled=false trên liên kết cũ; chọn lại dùng đúng bản ghi cũ. Không xóa liên kết hoặc ngành/phương thức/đợt.
6. Ngành, đợt và phương thức ngừng sử dụng không được chọn mới. Cấu hình cũ vẫn hiển thị để người quản trị xem và tắt nếu cần.
7. Các cột StartAt/EndAt là datetime2, được hiểu là giờ Việt Nam UTC+7. Khoảng mở là StartAt <= hiện tại < EndAt; StartAt bằng/sau EndAt bị từ chối. Đợt tắt hiển thị Ngừng sử dụng bất kể thời gian.

## Đối chiếu database

Đã đọc SQL Server tại `Server=.;Database=CNPM`: có các bảng Identity, Majors, AdmissionMethods, AdmissionRounds, RoundMajors, RoundMajorMethods và ApplicationStatuses. Hai migration InitialIdentity/AddSprint1BusinessEntities đã có trong lịch sử. Các trường và ràng buộc duy nhất cần cho Sprint 1 đã tồn tại.

Tài liệu thiết kế tổng thể mô tả 17 bảng cho bốn Sprint. Lần tích hợp này dùng phần Sprint 1, không dựng trước các chức năng của Sprint 2–4. Không cần chạy lệnh tạo lại bảng.

## Kết quả kiểm tra

- Build solution: thành công, 0 cảnh báo và 0 lỗi.
- Bộ HTTP `scripts/Test-Management.ps1`: **29/29 PASS**, chạy trên database riêng `CNPM_Sprint1_Test_20261102`, không dùng CNPM cho dữ liệu kiểm thử.
- Kiểm tra thời gian `tests/TimeRules`: **8/8 PASS**, gồm trước giờ mở, đúng giờ mở, ngay trước/đúng/sau giờ đóng, đợt tắt, hai mốc bằng nhau và chuẩn hóa mã.
- Đối chiếu SQL sau khi tắt cấu hình: bản ghi chỉ tiêu và liên kết phương thức cũ vẫn còn; liên kết chuyển IsEnabled=0.
- Giao diện quản trị và biểu mẫu cấu hình đã được kiểm tra trên trình duyệt, bao gồm chiều rộng điện thoại 390px.

Các ca HTTP bao gồm quản trị đọc đủ bốn màn hình; chặn khách, thí sinh và cán bộ ở cả GET/POST; CSRF; dữ liệu thiếu; mã ngành/phương thức/đợt trùng; cập nhật chính bản ghi; thời gian sai; chỉ tiêu âm/thập phân; phương thức giả mạo; chỉ tiêu 0; cặp ngành–đợt trùng; ẩn/sử dụng lại ngành; phương thức/đợt ngừng; lưu/tải lại; tắt cấu hình và giả mạo đổi cặp lịch sử; ID không tồn tại.

Bộ kiểm thử giữ dữ liệu trong database thử, báo cáo JSON ở `.local/management-verification.json`. Không chạy bộ kiểm thử ghi dữ liệu trên CNPM đang dùng để demo. Nếu cần chạy lại, đặt `ConnectionStrings__DefaultConnection` sang database thử, `Database__ApplyMigrationsOnStartup=true`, bật tài khoản demo rồi chạy website trước. Đây là bằng chứng kiểm thử tại máy hiện tại, chưa thay thế kiểm thử chéo của Hoàng/Thảo.

## Kịch bản demo

1. Khởi động SQL Server (MSSQLSERVER). Chạy `scripts/Start-Website.ps1` một lần để chuẩn bị tài khoản demo; lấy mật khẩu riêng tại `.local/demo-password.txt`.
2. Đăng nhập `admin.demo@tuyensinh.local`. Vào Không gian quản trị; chọn Ngành học.
3. Tạo ngành với mã mới, thử mã trùng, sửa tên, ẩn rồi sử dụng lại ngành.
4. Tạo phương thức xét tuyển có điều kiện; sửa điều kiện và thử mã trùng.
5. Tạo đợt tuyển sinh: thử thời gian sai rồi lưu thời gian đúng. Tại Cấu hình của đợt, chọn Thêm ngành vào đợt.
6. Chọn ngành, nhập chỉ tiêu và chọn một hoặc nhiều phương thức. Lưu, tải lại trang và sửa chỉ tiêu. Thử tạo lại đúng cặp ngành–đợt để minh họa chống trùng.
7. Ngừng sử dụng phương thức hoặc ngành; chỉ ra bản ghi và các liên kết cũ vẫn còn.
8. Đăng xuất rồi đăng nhập bằng thí sinh hoặc cán bộ, mở trực tiếp /Majors hay /Quotas để minh họa bị chặn.

Mỗi người cần chạy lại kịch bản phần mình, bổ sung ảnh/kết quả kiểm thử chéo và cập nhật Trello theo kết quả thực tế. Không đánh dấu nghiệm thu chỉ dựa trên việc đã có mã nguồn.

Tài liệu kỹ thuật: [MVC và EF Core CRUD](https://learn.microsoft.com/en-us/aspnet/core/data/ef-mvc/crud?view=aspnetcore-8.0), [kiểm tra dữ liệu biểu mẫu](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation?view=aspnetcore-9.0).
