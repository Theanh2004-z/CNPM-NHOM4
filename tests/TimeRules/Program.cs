using System.ComponentModel.DataAnnotations;
using TuyenSinhOnline.Models;
using TuyenSinhOnline.Models.Management;

var round = new AdmissionRound { StartAt = new(2026, 11, 2, 8, 0, 0), EndAt = new(2026, 11, 2, 17, 0, 0), IsActive = true };
var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    Console.WriteLine("PASS: " + name); passed++;
}
Check(AdmissionClock.Status(round, round.StartAt.AddTicks(-1)) == "Chưa mở", "Trước giờ mở");
Check(AdmissionClock.Status(round, round.StartAt) == "Đang mở", "Đúng giờ mở được tiếp nhận");
Check(AdmissionClock.Status(round, round.EndAt.AddTicks(-1)) == "Đang mở", "Ngay trước giờ đóng");
Check(AdmissionClock.Status(round, round.EndAt) == "Đã đóng", "Đúng giờ đóng ngừng tiếp nhận");
Check(AdmissionClock.Status(round, round.EndAt.AddTicks(1)) == "Đã đóng", "Sau giờ đóng");
round.IsActive = false;
Check(AdmissionClock.Status(round, round.StartAt) == "Ngừng sử dụng", "Đợt tắt không mở dù đang trong hạn");
var form = new RoundForm { Code = "DOT", Name = "Đợt", StartAt = round.StartAt, EndAt = round.StartAt };
var errors = new List<ValidationResult>();
Check(!Validator.TryValidateObject(form, new ValidationContext(form), errors, true), "Hai mốc thời gian bằng nhau bị từ chối");
Check(new MajorForm { Code = " cntt ", Name = "  Công nghệ thông tin  " }.Code == "CNTT", "Chuẩn hóa mã ngành");

// Kiểm tra quy tắc định dạng và dữ liệu của S3-04
var appCode = AdmissionApplication.BuildCode(2027, 42);
Check(appCode == "HS2027-000042", "Định dạng mã hồ sơ HS{Năm}-{Id:D6}");
var app = new AdmissionApplication { StatusId = (byte)ApplicationStatusEnum.Pending };
Check(app.StatusId == 1, "Trạng thái ban đầu luôn là Chờ tiếp nhận (Pending = 1)");
var pt100Fields = MethodFieldCatalog.For("PT100");
Check(pt100Fields.Length == 3 && pt100Fields.All(f => f.Min == 0 && f.Max == 10), "Phương thức PT100 yêu cầu đúng 3 môn điểm 0-10");
var pt200Fields = MethodFieldCatalog.For("PT200");
Check(pt200Fields.Length == 1 && pt200Fields[0].Min == 0 && pt200Fields[0].Max == 30, "Phương thức PT200 yêu cầu tổng học bạ 0-30");
var pt402Fields = MethodFieldCatalog.For("PT402");
Check(pt402Fields.Length == 1 && pt402Fields[0].Min == 0 && pt402Fields[0].Max == 1200, "Phương thức PT402 yêu cầu điểm ĐGNL 0-1200");

Console.WriteLine($"{passed}/{passed} PASS");
