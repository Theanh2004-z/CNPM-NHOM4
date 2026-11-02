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
Console.WriteLine($"{passed}/{passed} PASS");
