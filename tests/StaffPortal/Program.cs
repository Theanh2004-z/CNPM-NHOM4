using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Controllers;
using TuyenSinhOnline.Data;
using TuyenSinhOnline.Models;
using TuyenSinhOnline.Models.Management;
using TuyenSinhOnline.Security;

// Chỉ tạo và thay đổi dữ liệu trong database thử riêng của lần chạy này.
var database = "CNPM_Staff_Test_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "_" + Guid.NewGuid().ToString("N")[..4];
await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer($"Server=.;Database={database};Trusted_Connection=True;TrustServerCertificate=True").Options);
await db.Database.MigrateAsync();
ControllerContext Context(string role) => new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
    [new Claim(ClaimTypes.GivenName, "Người kiểm thử"), new Claim(ClaimTypes.Email, "staff@example.com"), new Claim(ClaimTypes.Role, role)], "test")) } };
var admin = new AdminController(db) { ControllerContext = Context(RoleNames.Administrator) };
var officer = new AdmissionsController(db) { ControllerContext = Context(RoleNames.AdmissionsOfficer) };
async Task<AdminHome> Admin() => (AdminHome)((ViewResult)await admin.Index()).Model!;
async Task<OfficerHome> Officer() => (OfficerHome)((ViewResult)await officer.Index()).Model!;
var passed = 0;
void Check(bool condition, string description) { if (!condition) throw new InvalidOperationException("FAIL: " + description); Console.WriteLine("PASS: " + description); passed++; }
async Task<string> Snapshot() => JsonSerializer.Serialize(new
{
    Majors = await db.Majors.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.Code, x.IsActive }).ToListAsync(),
    Rounds = await db.AdmissionRounds.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.StartAt, x.EndAt, x.IsActive }).ToListAsync(),
    Quotas = await db.RoundMajors.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.Quota, x.IsEnabled }).ToListAsync(),
    Methods = await db.AdmissionMethods.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.IsActive }).ToListAsync(),
    Links = await db.RoundMajorMethods.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.IsEnabled }).ToListAsync()
});
var summary = await Admin();
Check(summary.Majors.Total == 0 && summary.RecentRounds.Count == 0 && (await Officer()).Rounds.Count == 0, "Hai trang xử lý được database chưa có dữ liệu nghiệp vụ");
await Sprint1DemoData.SeedAsync(db);
var round = await db.AdmissionRounds.SingleAsync(x => x.Code == "TS2027-D1");
var next = await db.AdmissionRounds.SingleAsync(x => x.Code == "TS2027-D2");
round.StartAt = AdmissionClock.Now.AddDays(-1); round.EndAt = AdmissionClock.Now.AddDays(1); await db.SaveChangesAsync();
var before = await Snapshot(); summary = await Admin();
Check(summary.Majors == new CatalogCount(3, 3) && summary.Methods == new CatalogCount(3, 3)
    && summary.Rounds == new CatalogCount(2, 2) && summary.Configurations == new CatalogCount(3, 3) && summary.OpenRounds == 1,
    "Quản trị đếm đúng tổng số, trạng thái sử dụng và đợt đang mở từ SQL");
var plan = await Officer();
Check(plan.Rounds.Count == 2 && plan.Rounds.Single(x => x.Round.Id == round.Id).Majors.Single(x => x.Code == "7480201").Methods.Count == 2
    && plan.Rounds.Single(x => x.Round.Id == next.Id).Majors.Count == 0, "Cán bộ xem đúng kế hoạch từng đợt, không ghép nhầm ngành");
Check(summary.FullName == "Người kiểm thử" && plan.Email == "staff@example.com", "Thông tin tài khoản lấy từ phiên đăng nhập hiện tại");
Check(before == await Snapshot(), "Xem bảng điều hành và lịch không ghi dữ liệu SQL");
var english = await db.Majors.SingleAsync(x => x.Code == "7220201");
english.IsActive = false; await db.SaveChangesAsync();
summary = await Admin(); plan = await Officer();
Check(summary.Majors == new CatalogCount(3, 2) && summary.ConfigurationsToReview == 1
    && plan.Rounds.SelectMany(x => x.Majors).All(x => x.Code != english.Code), "Ngành ẩn biến mất ở cán bộ và được cảnh báo đúng ở quản trị");
english.IsActive = true;
var it = await db.Majors.SingleAsync(x => x.Code == "7480201");
var quota = await db.RoundMajors.SingleAsync(x => x.MajorId == it.Id && x.RoundId == round.Id);
quota.IsEnabled = false; await db.SaveChangesAsync();
Check((await Admin()).Configurations == new CatalogCount(3, 2)
    && (await Officer()).Rounds.SelectMany(x => x.Majors).All(x => x.Code != it.Code), "Cấu hình tắt được đếm riêng và không hiện với cán bộ");
quota.IsEnabled = true;
var transcript = await db.AdmissionMethods.SingleAsync(x => x.Code == "PT200");
transcript.IsActive = false; await db.SaveChangesAsync();
Check((await Officer()).Rounds.SelectMany(x => x.Majors).Single(x => x.Code == it.Code).Methods.Count == 1
    && (await Admin()).Methods == new CatalogCount(3, 2), "Phương thức ngừng sử dụng không xuất hiện trong kế hoạch cán bộ");
transcript.IsActive = true;
var link = await db.RoundMajorMethods.SingleAsync(x => x.RoundMajorId == quota.Id && x.MethodId == transcript.Id);
link.IsEnabled = false; await db.SaveChangesAsync();
Check((await Officer()).Rounds.SelectMany(x => x.Majors).Single(x => x.Code == it.Code).Methods.Count == 1, "Không hiện liên kết phương thức đã tắt");
var exam = await db.AdmissionMethods.SingleAsync(x => x.Code == "PT100");
exam.IsActive = false; await db.SaveChangesAsync();
Check((await Admin()).ConfigurationsToReview == 3 && (await Officer()).Rounds.All(x => x.Majors.Count == 0), "Thiếu phương thức hiệu lực được cảnh báo và không đưa ra kế hoạch cán bộ");
exam.IsActive = true; link.IsEnabled = true; round.IsActive = false; await db.SaveChangesAsync();
Check((await Admin()).RecentRounds.Any(x => x.Round.Id == round.Id && !x.Round.IsActive)
    && (await Officer()).Rounds.All(x => x.Round.Id != round.Id), "Đợt tắt vẫn có trong quản trị nhưng không hiển thị ở cán bộ");
for (var i = 0; i < 9; i++) db.AdmissionRounds.Add(new AdmissionRound { Code = "RECENT" + i, Name = "Đợt thử " + i,
    AdmissionYear = 2030, StartAt = new DateTime(2030, 1, 1).AddDays(i), EndAt = new DateTime(2030, 2, 1).AddDays(i) });
await db.SaveChangesAsync(); summary = await Admin();
Check(summary.RecentRounds.Count == 8 && summary.RecentRounds[0].Round.Code == "RECENT8"
    && summary.Rounds.Total == 11, "Danh sách quản trị giới hạn 8 đợt mới nhất nhưng tổng số vẫn đầy đủ");
Console.WriteLine($"{passed}/{passed} PASS"); Console.WriteLine("Database thử: " + database);
