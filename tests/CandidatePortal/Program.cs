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

// Tất cả thay đổi trong chương trình này chỉ dùng database thử vừa tạo riêng.
var database = "CNPM_Candidate_Test_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "_" + Guid.NewGuid().ToString("N")[..4];
await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer($"Server=.;Database={database};Trusted_Connection=True;TrustServerCertificate=True").Options);
await db.Database.MigrateAsync();
await Sprint1DemoData.SeedAsync(db);
var controller = new CandidateController(db)
{
    ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(
        new ClaimsIdentity([new Claim(ClaimTypes.GivenName, "Thí sinh kiểm thử"), new Claim(ClaimTypes.Email, "candidate@example.com"),
            new Claim(ClaimTypes.Role, RoleNames.Candidate)], "test")) } }
};
var passed = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + description);
    Console.WriteLine("PASS: " + description); passed++;
}
async Task<CandidateHome> Home(int? roundId = null, string? q = null) =>
    (CandidateHome)((ViewResult)await controller.Index(roundId, q)).Model!;
async Task<string> Snapshot() => JsonSerializer.Serialize(new
{
    Majors = await db.Majors.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.Code, x.Name, x.IsActive }).ToListAsync(),
    Rounds = await db.AdmissionRounds.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.StartAt, x.EndAt, x.IsActive }).ToListAsync(),
    Quotas = await db.RoundMajors.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.Quota, x.IsEnabled }).ToListAsync(),
    Methods = await db.AdmissionMethods.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.IsActive }).ToListAsync(),
    Links = await db.RoundMajorMethods.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.IsEnabled }).ToListAsync()
});
var round = await db.AdmissionRounds.SingleAsync(x => x.Code == "TS2027-D1");
var next = await db.AdmissionRounds.SingleAsync(x => x.Code == "TS2027-D2");
var it = await db.Majors.SingleAsync(x => x.Code == "7480201");
var quota = await db.RoundMajors.SingleAsync(x => x.RoundId == round.Id && x.MajorId == it.Id);
var transcript = await db.AdmissionMethods.SingleAsync(x => x.Code == "PT200");
var exam = await db.AdmissionMethods.SingleAsync(x => x.Code == "PT100");
var transcriptLink = await db.RoundMajorMethods.SingleAsync(x => x.RoundMajorId == quota.Id && x.MethodId == transcript.Id);

var before = await Snapshot();
var home = await Home(round.Id);
Check(home.Majors.Count == 3 && home.Majors.Single(x => x.Code == it.Code).Quota == 200
    && home.Majors.Single(x => x.Code == it.Code).Methods.Count == 2, "Đọc đúng ngành, chỉ tiêu và nhiều phương thức từ SQL");
Check(home.FullName == "Thí sinh kiểm thử" && home.Email == "candidate@example.com", "Thông tin tài khoản lấy từ người đang đăng nhập");
Check((await Home(round.Id, " 7480201 ")).Majors is [{ Code: "7480201" }], "Tìm mã ngành và bỏ khoảng trắng thừa");
Check((await Home(round.Id, "Quản trị")).Majors is [{ Code: "7340101" }], "Tìm tên ngành bằng tiếng Việt");
Check((await Home(round.Id, "KHONG_CO_NGANH")).Majors.Count == 0, "Tìm kiếm không khớp trả kết quả rỗng");
Check((await Home(next.Id)).Majors.Count == 0, "Đợt chưa cấu hình không lấy nhầm ngành của đợt khác");
Check(await controller.Index(int.MaxValue, null) is NotFoundResult, "Đợt không tồn tại trả 404");
Check(before == await Snapshot(), "Tra cứu và tìm kiếm không làm đổi dữ liệu SQL");

it.IsActive = false; await db.SaveChangesAsync();
Check((await Home(round.Id)).Majors.All(x => x.Code != it.Code), "Ngành đã ẩn không xuất hiện với thí sinh");
it.IsActive = true; quota.IsEnabled = false; await db.SaveChangesAsync();
Check((await Home(round.Id)).Majors.All(x => x.Code != it.Code), "Cấu hình chỉ tiêu đã tắt không xuất hiện");
quota.IsEnabled = true; transcript.IsActive = false; await db.SaveChangesAsync();
home = await Home(round.Id);
Check(home.Majors.Single(x => x.Code == it.Code).Methods is [{ Code: "PT100" }], "Phương thức ngừng sử dụng bị loại, phương thức còn hiệu lực vẫn hiện");
transcript.IsActive = true; transcriptLink.IsEnabled = false; await db.SaveChangesAsync();
Check((await Home(round.Id)).Majors.Single(x => x.Code == it.Code).Methods is [{ Code: "PT100" }], "Liên kết phương thức đã tắt không bị lộ");
exam.IsActive = false; await db.SaveChangesAsync();
Check((await Home(round.Id)).Majors.Count == 0, "Không quảng bá cấu hình không còn phương thức áp dụng");

var now = AdmissionClock.Now;
round.StartAt = now.AddDays(2); round.EndAt = now.AddDays(3);
next.StartAt = now.AddDays(5); next.EndAt = now.AddDays(6); await db.SaveChangesAsync();
Check((await Home()).SelectedRound?.Id == round.Id, "Mặc định chọn đợt sắp mở gần nhất");
next.StartAt = now.AddDays(-1); next.EndAt = now.AddDays(1); await db.SaveChangesAsync();
Check((await Home()).SelectedRound?.Id == next.Id, "Ưu tiên đợt đang mở hơn đợt tương lai");
next.IsActive = false; await db.SaveChangesAsync();
Check(await controller.Index(next.Id, null) is NotFoundResult && (await Home()).Rounds.All(x => x.Id != next.Id), "Đợt ngừng sử dụng không thể truy cập bằng ID hoặc danh sách");
round.IsActive = false; await db.SaveChangesAsync();
home = await Home();
Check(home.SelectedRound is null && home.Rounds.Count == 0 && home.Majors.Count == 0, "Chưa có đợt đang sử dụng trả trạng thái rỗng an toàn");
Console.WriteLine($"{passed}/{passed} PASS");
Console.WriteLine("Database thử: " + database);
