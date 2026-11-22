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
var connStr = $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True";
try
{
    using var testConn = new Microsoft.Data.SqlClient.SqlConnection("Server=.;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=1");
    testConn.Open();
    connStr = $"Server=.;Database={database};Trusted_Connection=True;TrustServerCertificate=True";
}
catch { }

await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer(connStr).Options);
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

// ====================================================================
// SPRINT 4: KIỂM THỬ TỰ ĐỘNG S4-02, S4-03, S4-05, S4-06
// ====================================================================

// 1. Seed trạng thái và quy tắc chuyển đổi trạng thái nếu chưa có
foreach (var rule in ApplicationWorkflowRules.Transitions)
{
    var fromStatus = (byte)rule.From;
    var toStatus = (byte)rule.To;
    if (!await db.ApplicationStatusTransitions.AnyAsync(t => t.FromStatusId == fromStatus && t.ToStatusId == toStatus))
    {
        db.ApplicationStatusTransitions.Add(new ApplicationStatusTransition
        {
            FromStatusId = fromStatus,
            ToStatusId = toStatus,
            RequiresNote = rule.RequiresNote
        });
    }
}
var wantedStatuses = new[]
{
    (Id: (byte)1, Code: "Pending", Name: "Chờ tiếp nhận"),
    (Id: (byte)2, Code: "InReview", Name: "Đang xét duyệt"),
    (Id: (byte)3, Code: "NeedsSupplement", Name: "Cần bổ sung"),
    (Id: (byte)4, Code: "Approved", Name: "Đã duyệt"),
    (Id: (byte)5, Code: "Rejected", Name: "Từ chối")
};
foreach (var s in wantedStatuses)
{
    if (!await db.ApplicationStatuses.AnyAsync(x => x.Id == s.Id))
        db.ApplicationStatuses.Add(new ApplicationStatus { Id = s.Id, Code = s.Code, Name = s.Name });
}
await db.SaveChangesAsync();

// 2. Tạo tài khoản Thí sinh, Cán bộ và Quản trị viên
var testCandidate = new ApplicationUser
{
    Id = Guid.NewGuid().ToString(),
    UserName = "candidate.stafftest@example.com",
    Email = "candidate.stafftest@example.com",
    FullName = "Nguyễn Văn Thí Sinh Test"
};
var testOfficer = new ApplicationUser
{
    Id = Guid.NewGuid().ToString(),
    UserName = "officer.stafftest@example.com",
    Email = "officer.stafftest@example.com",
    FullName = "Trần Cán Bộ Tuyển Sinh"
};
var testAdmin = new ApplicationUser
{
    Id = Guid.NewGuid().ToString(),
    UserName = "admin.stafftest@example.com",
    Email = "admin.stafftest@example.com",
    FullName = "Lê Quản Trị Hệ Thống"
};
db.Users.AddRange(testCandidate, testOfficer, testAdmin);
round.IsActive = true;
await db.SaveChangesAsync();

// 3. Tạo hồ sơ xét tuyển mẫu ở trạng thái Pending (1)
var app1 = new AdmissionApplication
{
    UserId = testCandidate.Id,
    RoundMajorMethodId = link.Id,
    ApplicationCode = "HS2027-999001",
    FullName = testCandidate.FullName,
    DateOfBirth = new DateOnly(2008, 1, 1),
    PhoneNumber = "0912345678",
    ContactEmail = testCandidate.Email,
    SchoolName = "THPT Chuyên Test",
    GraduationYear = 2026,
    StatusId = (byte)ApplicationStatusEnum.Pending,
    SubmittedAt = AdmissionClock.Now
};
db.AdmissionApplications.Add(app1);
await db.SaveChangesAsync();

var staffCtrl = new StaffApplicationsController(db)
{
    ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, testOfficer.Id),
                new Claim(ClaimTypes.GivenName, testOfficer.FullName),
                new Claim(ClaimTypes.Role, RoleNames.AdmissionsOfficer)
            ], "test"))
        }
    }
};

// S4-02: Tra cứu & Lọc hồ sơ
var searchByCode = (StaffApplicationListViewModel)((ViewResult)await staffCtrl.Index("999001", null, null)).Model!;
Check(searchByCode.Items.Count == 1 && searchByCode.Items[0].Id == app1.Id,
    "S4-02: Tìm kiếm chính xác theo mã hồ sơ thành công");

var searchByName = (StaffApplicationListViewModel)((ViewResult)await staffCtrl.Index("Thí Sinh Test", null, null)).Model!;
Check(searchByName.Items.Count == 1 && searchByName.Items[0].Id == app1.Id,
    "S4-02: Tìm kiếm theo họ tên thí sinh thành công");

var filterByMajor = (StaffApplicationListViewModel)((ViewResult)await staffCtrl.Index(null, it.Id, null)).Model!;
Check(filterByMajor.Items.Count == 1, "S4-02: Lọc theo ngành có hồ sơ thành công");

var filterByOtherMajor = (StaffApplicationListViewModel)((ViewResult)await staffCtrl.Index(null, english.Id, null)).Model!;
Check(filterByOtherMajor.Items.Count == 0, "S4-02: Lọc ngành chưa có hồ sơ trả về danh sách rỗng");

var filterByStatus = (StaffApplicationListViewModel)((ViewResult)await staffCtrl.Index(null, null, (byte)ApplicationStatusEnum.Pending)).Model!;
Check(filterByStatus.Items.Count == 1, "S4-02: Lọc theo trạng thái Chờ tiếp nhận thành công");

var filterCombine = (StaffApplicationListViewModel)((ViewResult)await staffCtrl.Index("999001", it.Id, (byte)ApplicationStatusEnum.Pending)).Model!;
Check(filterCombine.Items.Count == 1 && filterCombine.Search == "999001" && filterCombine.MajorId == it.Id && filterCombine.StatusId == 1,
    "S4-02: Kết hợp tìm kiếm + lọc bảo toàn đầy đủ các điều kiện");

// Dropdown ngành giữ nguyên cả ngành ngừng hoạt động
Check(searchByCode.Majors.Any(m => m.Id == english.Id),
    "S4-02: Dropdown ngành giữ nguyên tất cả ngành để cán bộ tra cứu hồ sơ cũ");

// S4-03: Kiểm thử máy trạng thái và chuyển trạng thái
// 1. Chặn chuyển sai quy trình: Pending -> Approved bị từ chối
var invalidJump = await ApplicationWorkflow.ChangeStatusAsync(db, app1.Id, ApplicationStatusEnum.Approved, testOfficer.Id, null);
Check(!invalidJump.Ok, "S4-03: Chặn chuyển trạng thái sai quy trình (Pending -> Approved bị từ chối)");

// 2. Chuyển hợp lệ: Pending -> InReview
var step1 = await ApplicationWorkflow.ChangeStatusAsync(db, app1.Id, ApplicationStatusEnum.InReview, testOfficer.Id, null);
Check(step1.Ok, "S4-03: Chuyển hợp lệ Pending -> InReview thành công");

// 3. Chặn thiếu lý do khi yêu cầu bổ sung: InReview -> NeedsSupplement với note rỗng
var emptyNoteStep = await ApplicationWorkflow.ChangeStatusAsync(db, app1.Id, ApplicationStatusEnum.NeedsSupplement, testOfficer.Id, "   ");
Check(!emptyNoteStep.Ok, "S4-03: Bắt buộc nhập lý do khi Yêu cầu bổ sung (note rỗng bị từ chối)");

// 4. Chuyển hợp lệ: InReview -> NeedsSupplement kèm lý do công khai
var step2 = await ApplicationWorkflow.ChangeStatusAsync(db, app1.Id, ApplicationStatusEnum.NeedsSupplement, testOfficer.Id, "Bổ sung bản photo công chứng CCCD");
Check(step2.Ok, "S4-03: Yêu cầu bổ sung kèm lý do thành công");

// 5. Chuyển hợp lệ: NeedsSupplement -> InReview kèm ghi chú nội bộ
var step3 = await ApplicationWorkflow.ChangeStatusAsync(db, app1.Id, ApplicationStatusEnum.InReview, testOfficer.Id, "Ghi chú nội bộ: Đã nhận bổ sung qua bưu điện");
Check(step3.Ok, "S4-03: Xác nhận nhận bổ sung quay lại xét duyệt thành công");

// 6. Chuyển hợp lệ: InReview -> Approved kèm ghi chú nội bộ
var step4 = await ApplicationWorkflow.ChangeStatusAsync(db, app1.Id, ApplicationStatusEnum.Approved, testOfficer.Id, "Ghi chú nội bộ: Đạt điểm xét tuyển");
Check(step4.Ok, "S4-03: Duyệt hồ sơ thành công");

// 7. Kiểm tra lịch sử lưu đủ 4 bản ghi theo thứ tự thời gian, không ghi đè
var histories = await db.ApplicationStatusHistories.Where(h => h.ApplicationId == app1.Id).OrderBy(h => h.ChangedAt).ToListAsync();
Check(histories.Count == 4, "S4-03: Lưu đủ toàn bộ 4 bản ghi lịch sử, không ghi đè bản ghi cũ");

// 8. Chống ghi đè concurrency: kiểm tra expectedStatusId
var staleChange = await ApplicationWorkflow.ChangeStatusAsync(db, app1.Id, ApplicationStatusEnum.InReview, testOfficer.Id, "Test", expectedFromStatusId: (byte)ApplicationStatusEnum.Pending);
Check(!staleChange.Ok, "S4-03: Chống ghi đè khi trạng thái mong đợi bị lệch (Concurrency check)");

// Phân quyền chi tiết cán bộ vs quản trị viên
var officerDetails = (StaffApplicationDetailViewModel)((ViewResult)await staffCtrl.Details(app1.Id)).Model!;
Check(officerDetails.CanProcess == true, "S4-02: Cán bộ tuyển sinh có quyền xử lý hồ sơ (CanProcess == true)");

var adminStaffCtrl = new StaffApplicationsController(db)
{
    ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, testAdmin.Id),
                new Claim(ClaimTypes.GivenName, testAdmin.FullName),
                new Claim(ClaimTypes.Role, RoleNames.Administrator)
            ], "test"))
        }
    }
};
var adminDetails = (StaffApplicationDetailViewModel)((ViewResult)await adminStaffCtrl.Details(app1.Id)).Model!;
Check(adminDetails.CanProcess == false, "S4-02: Quản trị viên chỉ có quyền xem chi tiết (CanProcess == false)");

// ====================================================================
// SPRINT 4 (S4-05 & S4-06): THỐNG KÊ TUYỂN SINH
// ====================================================================

// S4-05: Thống kê chính xác chống đếm trùng (1 hồ sơ có 4 lịch sử chỉ đếm là 1)
var statsRound1 = (StaffStatisticsViewModel)((ViewResult)await staffCtrl.Statistics(round.Id)).Model!;
Check(statsRound1.TotalApplications == 1,
    "S4-05: Chống đếm trùng tuyệt đối: Hồ sơ có 4 bản ghi lịch sử chỉ được tính là ĐÚNG 1 hồ sơ");

Check(statsRound1.MajorRows.Count == 1 && statsRound1.MajorRows[0].Count == 1 && statsRound1.MajorRows[0].Percentage == 100.0,
    "S4-05: Thống kê theo ngành chính xác 100%");

Check(statsRound1.MethodRows.Count == 1 && statsRound1.MethodRows[0].Count == 1 && statsRound1.MethodRows[0].Percentage == 100.0,
    "S4-05: Thống kê theo phương thức chính xác 100%");

// Luôn hiển thị đủ 5 trạng thái gốc
Check(statsRound1.StatusRows.Count == 5,
    "S4-05: Bảng trạng thái luôn hiển thị đủ 5 trạng thái kể cả khi số lượng = 0");

var approvedStat = statsRound1.StatusRows.Single(s => s.StatusId == (byte)ApplicationStatusEnum.Approved);
Check(approvedStat.Count == 1 && approvedStat.Percentage == 100.0,
    "S4-05: Trạng thái Đã duyệt đếm đúng 1 hồ sơ (100%)");

var pendingStat = statsRound1.StatusRows.Single(s => s.StatusId == (byte)ApplicationStatusEnum.Pending);
Check(pendingStat.Count == 0 && pendingStat.Percentage == 0.0,
    "S4-05: Trạng thái Chờ tiếp nhận hiển thị đúng 0 hồ sơ (0%) không bị mất dòng");

// Đổi sang đợt 2 (đợt 0 hồ sơ) an toàn không chia cho 0
var statsRound2 = (StaffStatisticsViewModel)((ViewResult)await staffCtrl.Statistics(next.Id)).Model!;
Check(statsRound2.TotalApplications == 0 && statsRound2.MajorRows.Count == 0 && statsRound2.MethodRows.Count == 0,
    "S4-05: Đổi sang đợt 0 hồ sơ trả về số lượng 0 an toàn không lỗi chia cho 0");
Check(statsRound2.StatusRows.Count == 5 && statsRound2.StatusRows.All(s => s.Count == 0 && s.Percentage == 0.0),
    "S4-05: Đợt 0 hồ sơ vẫn hiển thị đủ 5 trạng thái đều bằng 0");

// Quản trị viên cũng mở được trang Thống kê
var adminStats = (StaffStatisticsViewModel)((ViewResult)await adminStaffCtrl.Statistics(round.Id)).Model!;
Check(adminStats.TotalApplications == 1,
    "S4-06: Quản trị viên truy cập và xem được số liệu Thống kê tuyển sinh");
Console.WriteLine($"{passed}/{passed} PASS"); Console.WriteLine("Database thử: " + database);
