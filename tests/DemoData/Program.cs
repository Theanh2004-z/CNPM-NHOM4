using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Data;

var database = "CNPM_Sprint1_Fix_Seed_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "_" + Guid.NewGuid().ToString("N")[..4];
var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer($"Server=.;Database={database};Trusted_Connection=True;TrustServerCertificate=True").Options;
await using var db = new ApplicationDbContext(options);
await db.Database.MigrateAsync();
var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    Console.WriteLine("PASS: " + name); passed++;
}
async Task<string> Snapshot()
{
    var majors = await db.Majors.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.Code, x.Name, x.IsActive }).ToListAsync();
    var methods = await db.AdmissionMethods.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.Code, x.Name, x.IsActive }).ToListAsync();
    var rounds = await db.AdmissionRounds.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.Code, x.StartAt, x.EndAt, x.IsActive }).ToListAsync();
    var quotas = await db.RoundMajors.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.RoundId, x.MajorId, x.Quota, x.IsEnabled }).ToListAsync();
    var links = await db.RoundMajorMethods.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.RoundMajorId, x.MethodId, x.IsEnabled }).ToListAsync();
    return System.Text.Json.JsonSerializer.Serialize(new { majors, methods, rounds, quotas, links });
}

await Sprint1DemoData.SeedAsync(db);
Check(await db.Majors.CountAsync() == 3 && await db.AdmissionMethods.CountAsync() == 3
    && await db.AdmissionRounds.CountAsync() == 2 && await db.RoundMajors.CountAsync() == 3
    && await db.RoundMajorMethods.CountAsync() == 4, "Database mới có đủ danh mục, đợt, chỉ tiêu và liên kết mẫu");
var before = await Snapshot();
await Sprint1DemoData.SeedAsync(db);
Check(before == await Snapshot(), "Seed lần hai không tạo trùng và không đổi dữ liệu");

var it = await db.Majors.SingleAsync(x => x.Code == "7480201");
var round = await db.AdmissionRounds.SingleAsync(x => x.Code == "TS2027-D1");
var itQuota = await db.RoundMajors.SingleAsync(x => x.MajorId == it.Id && x.RoundId == round.Id);
var transcript = await db.AdmissionMethods.SingleAsync(x => x.Code == "PT200");
var transcriptLink = await db.RoundMajorMethods.SingleAsync(x => x.RoundMajorId == itQuota.Id && x.MethodId == transcript.Id);
it.Name = "Tên ngành đã tùy chỉnh"; itQuota.Quota = 77; round.EndAt = round.EndAt.AddDays(2);
transcript.IsActive = false; transcriptLink.IsEnabled = false;
await db.SaveChangesAsync();
before = await Snapshot();
await Sprint1DemoData.SeedAsync(db);
Check(before == await Snapshot(), "Không ghi đè tên, chỉ tiêu, thời gian hoặc tự bật lại phương thức/liên kết đã tắt");

var english = await db.Majors.SingleAsync(x => x.Code == "7220201");
var englishQuota = await db.RoundMajors.SingleAsync(x => x.RoundId == round.Id && x.MajorId == english.Id);
english.IsActive = false; englishQuota.IsEnabled = false;
await db.SaveChangesAsync();
before = await Snapshot();
await Sprint1DemoData.SeedAsync(db);
Check(before == await Snapshot(), "Không tự mở lại ngành hoặc cấu hình đã ngừng sử dụng");

// Mô phỏng riêng bộ dữ liệu cũ trên database thử do chương trình này vừa tạo.
english.IsActive = true; englishQuota.IsEnabled = true;
var exam = await db.AdmissionMethods.SingleAsync(x => x.Code == "PT100");
var englishLink = await db.RoundMajorMethods.SingleAsync(x => x.RoundMajorId == englishQuota.Id && x.MethodId == exam.Id);
db.RoundMajorMethods.Remove(englishLink);
await db.SaveChangesAsync();
await Sprint1DemoData.SeedAsync(db);
Check(await db.RoundMajorMethods.AnyAsync(x => x.RoundMajorId == englishQuota.Id && x.MethodId == exam.Id && x.IsEnabled)
    && itQuota.Quota == 77, "Bổ sung liên kết PT100 còn thiếu cho Ngôn ngữ Anh, giữ nguyên chỉ tiêu đã chỉnh");
Console.WriteLine($"{passed}/{passed} PASS");
Console.WriteLine("Database thử: " + database);
