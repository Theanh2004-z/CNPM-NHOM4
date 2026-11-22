using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TuyenSinhOnline.Controllers;
using TuyenSinhOnline.Data;
using TuyenSinhOnline.Models;
using TuyenSinhOnline.Models.Management;
using TuyenSinhOnline.Security;

// Tất cả thay đổi trong chương trình này chỉ dùng database thử vừa tạo riêng.
var database = "CNPM_Candidate_Test_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "_" + Guid.NewGuid().ToString("N")[..4];
var connStr = $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True";
try
{
    using var testConn = new Microsoft.Data.SqlClient.SqlConnection("Server=.;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=1");
    testConn.Open();
    connStr = $"Server=.;Database={database};Trusted_Connection=True;TrustServerCertificate=True";
}
catch { }

var services = new ServiceCollection();
services.AddLogging();
services.AddControllersWithViews();
services.AddDbContext<ApplicationDbContext>(opt => opt.UseSqlServer(connStr));
services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
var sp = services.BuildServiceProvider();

await using var db = sp.GetRequiredService<ApplicationDbContext>();
await db.Database.MigrateAsync();
await DatabaseInitializer.InitializeAsync(sp, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
    new Microsoft.Extensions.Hosting.Internal.HostingEnvironment { EnvironmentName = "Development" }, false);
await Sprint1DemoData.SeedAsync(db);

var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
var tempDataProvider = sp.GetRequiredService<ITempDataProvider>();

var controller = new CandidateController(db)
{
    ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.GivenName, "Thí sinh kiểm thử"),
                new Claim(ClaimTypes.Email, "candidate@example.com"),
                new Claim(ClaimTypes.Role, RoleNames.Candidate)
            ], "test"))
        }
    }
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

// ==========================================
// KIỂM THỬ SPRINT 3 (S3-03, S3-04, S3-05)
// ==========================================
round.IsActive = true;
round.StartAt = now.AddDays(-1);
round.EndAt = now.AddDays(1);
it.IsActive = true;
quota.IsEnabled = true;
transcript.IsActive = true;
transcriptLink.IsEnabled = true;
exam.IsActive = true;
await db.SaveChangesAsync();

// 1. Tạo 2 tài khoản thí sinh để kiểm thử bảo mật & IDOR
var testUserA = new ApplicationUser { UserName = "candidate_a@test.local", Email = "candidate_a@test.local", FullName = "Nguyễn Văn A" };
await userManager.CreateAsync(testUserA, "Test@123456");
await userManager.AddToRoleAsync(testUserA, RoleNames.Candidate);

var testUserB = new ApplicationUser { UserName = "candidate_b@test.local", Email = "candidate_b@test.local", FullName = "Trần Thị B" };
await userManager.CreateAsync(testUserB, "Test@123456");
await userManager.AddToRoleAsync(testUserB, RoleNames.Candidate);

ApplicationFormController CreateAppController(ApplicationUser user)
{
    var httpContext = new DefaultHttpContext
    {
        User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Email!),
            new Claim(ClaimTypes.Role, RoleNames.Candidate)
        ], "test"))
    };
    return new ApplicationFormController(db, userManager)
    {
        ControllerContext = new ControllerContext { HttpContext = httpContext },
        TempData = new TempDataDictionary(httpContext, tempDataProvider)
    };
}

var appCtrlA = CreateAppController(testUserA);

// S3-03: Kiểm tra điều kiện phía server (TC03, TC04, TC11)
var getCreate = (ViewResult)await appCtrlA.Create(roundId: null, CancellationToken.None);
var pageModel = (ApplicationFormPage)getCreate.Model!;
Check(pageModel.IncompleteProfile, "S3-03: Thí sinh A chưa có hồ sơ báo IncompleteProfile (TC03)");

var postFailNoProfile = (ViewResult)await appCtrlA.Create(new ApplicationFormInput
{
    RoundId = round.Id, RoundMajorId = quota.Id, RoundMajorMethodId = transcriptLink.Id
}, CancellationToken.None);
Check(!appCtrlA.ModelState.IsValid, "S3-03: Chặn nộp hồ sơ khi chưa có hồ sơ cá nhân (TC03)");

// Tạo Profile nhưng chưa có Education
var profileA = new CandidateProfile
{
    UserId = testUserA.Id,
    FullName = "Nguyễn Văn A",
    DateOfBirth = new DateOnly(2008, 1, 1),
    PhoneNumber = "0912345678",
    ContactEmail = "candidate_a@test.local",
    UpdatedAt = DateTime.UtcNow
};
db.CandidateProfiles.Add(profileA);
await db.SaveChangesAsync();

appCtrlA.ModelState.Clear();
var postFailNoEdu = (ViewResult)await appCtrlA.Create(new ApplicationFormInput
{
    RoundId = round.Id, RoundMajorId = quota.Id, RoundMajorMethodId = transcriptLink.Id
}, CancellationToken.None);
Check(!appCtrlA.ModelState.IsValid, "S3-03: Chặn nộp hồ sơ khi thiếu học vấn THPT (TC04)");

// Cập nhật đủ học vấn THPT
profileA.Education = new CandidateEducation
{
    CandidateProfileId = profileA.Id,
    SchoolName = "THPT Chuyên Test",
    GraduationYear = 2026
};
await db.SaveChangesAsync();

// Kiểm tra điểm ngoài miền quy định (TongHocBa max = 30)
appCtrlA.ModelState.Clear();
var postFailScore = (ViewResult)await appCtrlA.Create(new ApplicationFormInput
{
    RoundId = round.Id,
    RoundMajorId = quota.Id,
    RoundMajorMethodId = transcriptLink.Id,
    Scores = { ["TongHocBa"] = "35" }
}, CancellationToken.None);
Check(!appCtrlA.ModelState.IsValid, "S3-03: Điểm vượt quá miền cho phép bị từ chối (TC11)");

// S3-04: Nộp hồ sơ hợp lệ, sinh mã, lưu snapshot, trạng thái ban đầu (TC15, TC16, TC17, TC18)
appCtrlA.ModelState.Clear();
var postSuccess = await appCtrlA.Create(new ApplicationFormInput
{
    RoundId = round.Id,
    RoundMajorId = quota.Id,
    RoundMajorMethodId = transcriptLink.Id,
    Scores = { ["TongHocBa"] = "28.5" }
}, CancellationToken.None);

Check(postSuccess is RedirectToActionResult redirect && redirect.ActionName == "Details",
    "S3-04: Nộp thành công chuyển hướng PRG sang Details (TC15, TC20)");

var appId = (int)((RedirectToActionResult)postSuccess).RouteValues!["id"]!;
var savedApp = await db.AdmissionApplications.Include(a => a.Scores).SingleAsync(a => a.Id == appId);
Check(savedApp.ApplicationCode.StartsWith("HS2027-"), "S3-04: Mã hồ sơ đúng định dạng HS{Năm}-{Số} (TC16)");
Check(savedApp.StatusId == 1, "S3-04: Trạng thái ban đầu là Chờ tiếp nhận (TC17)");
Check(savedApp.Scores.Single().Value == 28.5m, "S3-04: Điểm xét tuyển được lưu chính xác");

// Snapshot bất biến: Sửa Profile không làm đổi Application (TC18)
profileA.FullName = "Nguyễn Văn A (Tên Đã Đổi)";
await db.SaveChangesAsync();
var reloadedApp = await db.AdmissionApplications.AsNoTracking().SingleAsync(a => a.Id == appId);
Check(reloadedApp.FullName == "Nguyễn Văn A", "S3-04: Snapshot bất biến độc lập với CandidateProfile (TC18)");

// Chống nộp lặp (TC19)
appCtrlA.ModelState.Clear();
var postDup = (ViewResult)await appCtrlA.Create(new ApplicationFormInput
{
    RoundId = round.Id,
    RoundMajorId = quota.Id,
    RoundMajorMethodId = transcriptLink.Id,
    Scores = { ["TongHocBa"] = "28.5" }
}, CancellationToken.None);
Check(!appCtrlA.ModelState.IsValid, "S3-04: Chống gửi lặp cùng nguyện vọng thành công (TC19)");

// S3-05: Danh sách, Chi tiết và Chống IDOR (TC23, TC24, TC26, TC27)
var listResA = (ViewResult)await appCtrlA.Index(null, null, CancellationToken.None);
var listModelA = (ApplicationListViewModel)listResA.Model!;
Check(listModelA.Applications.Count == 1 && listModelA.Applications[0].Id == appId,
    "S3-05: Thí sinh A xem được danh sách hồ sơ của mình (TC24)");

var detailsResA = (ViewResult)await appCtrlA.Details(appId, CancellationToken.None);
Check(detailsResA.Model is AdmissionApplication, "S3-05: Thí sinh A xem được chi tiết hồ sơ snapshot (TC26)");

// Thí sinh B truy cập hồ sơ của thí sinh A -> Bị chặn 404 (Chống IDOR)
var appCtrlB = CreateAppController(testUserB);
var idorRes = await appCtrlB.Details(appId, CancellationToken.None);
Check(idorRes is NotFoundResult, "S3-05: Thí sinh B bị chặn 404 khi truy cập hồ sơ Thí sinh A - Chống IDOR thành công (TC27)");

var listResB = (ViewResult)await appCtrlB.Index(null, null, CancellationToken.None);
var listModelB = (ApplicationListViewModel)listResB.Model!;
Check(listModelB.TotalCount == 0, "S3-05: Thí sinh B có danh sách rỗng độc lập (TC23)");

// Kiểm tra Candidate/Index cập nhật số lượng hồ sơ thật (TC29)
var candidateControllerA = new CandidateController(db)
{
    ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, testUserA.Id),
                new Claim(ClaimTypes.GivenName, testUserA.FullName),
                new Claim(ClaimTypes.Role, RoleNames.Candidate)
            ], "test"))
        }
    }
};
var candHomeA = (CandidateHome)((ViewResult)await candidateControllerA.Index(round.Id, null)).Model!;
Check(candHomeA.ApplicationCount == 1 && candHomeA.RecentApplications.Count == 1,
    "S3-05: Cổng thí sinh Candidate/Index cập nhật số lượng hồ sơ thật (TC29)");

// ====================================================================
// SPRINT 4 (S4-04): KIỂM TRA HIỂN THỊ TRẠNG THÁI MỚI & ẨN GHI CHÚ NỘI BỘ
// ====================================================================

// Tạo tài khoản cán bộ tuyển sinh trong AspNetUsers để thỏa mãn ràng buộc khóa ngoại ChangedByUserId
var officerUser = new ApplicationUser
{
    Id = Guid.NewGuid().ToString(),
    UserName = "officer.test@example.com",
    NormalizedUserName = "OFFICER.TEST@EXAMPLE.COM",
    Email = "officer.test@example.com",
    NormalizedEmail = "OFFICER.TEST@EXAMPLE.COM",
    FullName = "Cán bộ Tuyển sinh Test"
};
db.Users.Add(officerUser);
await db.SaveChangesAsync();

// Giả lập Cán bộ tuyển sinh xử lý hồ sơ qua chuỗi máy trạng thái 5 bước
var officerUserId = officerUser.Id;
await ApplicationWorkflow.ChangeStatusAsync(db, appId, ApplicationStatusEnum.InReview, officerUserId, null);
await ApplicationWorkflow.ChangeStatusAsync(db, appId, ApplicationStatusEnum.NeedsSupplement, officerUserId, "Thiếu bản sao học bạ công chứng");
await ApplicationWorkflow.ChangeStatusAsync(db, appId, ApplicationStatusEnum.InReview, officerUserId, "Ghi chú nội bộ: Đã nhận đủ bổ sung qua bưu điện");
await ApplicationWorkflow.ChangeStatusAsync(db, appId, ApplicationStatusEnum.Approved, officerUserId, "Ghi chú nội bộ: Đạt điểm xét tuyển ngành CNTT");

// Thí sinh A mở chi tiết hồ sơ sau khi được duyệt
var detailsAfterReview = (ViewResult)await appCtrlA.Details(appId, CancellationToken.None);
var appAfterReview = (AdmissionApplication)detailsAfterReview.Model!;
Check(appAfterReview.StatusId == (byte)ApplicationStatusEnum.Approved,
    "S4-04: Thí sinh xem được trạng thái mới nhất 'Đã duyệt' sau khi cán bộ xử lý");

var candHistory = (List<CandidateHistoryRow>)detailsAfterReview.ViewData["History"]!;
Check(candHistory.Count == 4, "S4-04: Thí sinh thấy đủ 4 mốc lịch sử thay đổi trạng thái");

// Mốc 1: Pending -> InReview: Note phải là null
Check(candHistory[0].ToStatus == "Đang xét duyệt" && candHistory[0].Note == null,
    "S4-04: Bước InReview ban đầu không lộ ghi chú (Note == null)");

// Mốc 2: InReview -> NeedsSupplement: Thí sinh PHẢI thấy lý do
Check(candHistory[1].ToStatus == "Cần bổ sung" && candHistory[1].Note == "Thiếu bản sao học bạ công chứng",
    "S4-04: Thí sinh thấy rõ lý do công khai khi được yêu cầu bổ sung");

// Mốc 3: NeedsSupplement -> InReview: Ghi chú nội bộ của cán bộ TUYỆT ĐỐI BỊ ẨN
Check(candHistory[2].ToStatus == "Đang xét duyệt" && candHistory[2].Note == null,
    "S4-04: Tuyệt đối KHÔNG lộ ghi chú nội bộ khi xác nhận nhận bổ sung (Note == null)");

// Mốc 4: InReview -> Approved: Ghi chú nội bộ khi duyệt TUYỆT ĐỐI BỊ ẨN
Check(candHistory[3].ToStatus == "Đã duyệt" && candHistory[3].Note == null,
    "S4-04: Tuyệt đối KHÔNG lộ ghi chú nội bộ khi duyệt hồ sơ (Note == null)");

// Kiểm tra bảo vệ IDOR vẫn hoạt động tuyệt đối sau khi duyệt
var idorAfterReview = await appCtrlB.Details(appId, CancellationToken.None);
Check(idorAfterReview is NotFoundResult,
    "S4-04: Thí sinh B vẫn bị chặn 404 khi truy cập hồ sơ Thí sinh A sau khi đã duyệt");

Console.WriteLine($"\n====================================");
Console.WriteLine($"TỔNG KẾT: {passed}/{passed} TẤT CẢ TEST CASES ĐÃ ĐẠT (PASS 100%)");
Console.WriteLine($"Database kiểm thử: {database}");
Console.WriteLine($"====================================\n");
