using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Data;
using TuyenSinhOnline.Models;
using TuyenSinhOnline.Models.Management;
using TuyenSinhOnline.Security;

namespace TuyenSinhOnline.Controllers;

// S3-02: Biểu mẫu nộp hồ sơ xét tuyển (chỉ thí sinh).
// Việc LƯU hồ sơ (S3-03) và SINH MÃ (S3-04) sẽ nối vào chỗ có TODO bên dưới.
[Authorize(Roles = RoleNames.Candidate)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ApplicationFormController(ApplicationDbContext db, UserManager<ApplicationUser> users) : Controller
{
    // Hiển thị form
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var page = await LoadPageAsync(new ApplicationFormInput(), ct);
        return page is null ? Challenge() : View(page);
    }

    // Nhận dữ liệu form: kiểm tra lỗi ở máy chủ
    [HttpPost]
    public async Task<IActionResult> Create(ApplicationFormInput input, CancellationToken ct)
    {
        var page = await LoadPageAsync(input, ct);
        if (page is null) return Challenge();
        if (page.Profile is null || page.Round is null) return View(page); // không có gì để gửi

        // 1) Ngành phải nằm trong danh sách ngành đang mở
        var major = page.Majors.FirstOrDefault(m => m.RoundMajorId == input.RoundMajorId);
        if (major is null)
            ModelState.AddModelError("RoundMajorId", "Vui lòng chọn ngành.");

        // 2) Phương thức phải thuộc đúng ngành đã chọn
        var method = major?.Methods.FirstOrDefault(m => m.RoundMajorMethodId == input.RoundMajorMethodId);
        if (major is not null && method is null)
            ModelState.AddModelError("RoundMajorMethodId", "Vui lòng chọn phương thức áp dụng cho ngành này.");

        // 3) Mỗi trường điểm của phương thức phải là số trong khoảng cho phép
        if (method is not null)
        {
            foreach (var field in method.Fields)
            {
                var text = input.Scores.GetValueOrDefault(field.Key)?.Trim().Replace(',', '.') ?? "";
                var key = $"Scores[{field.Key}]";
                if (text.Length == 0)
                    ModelState.AddModelError(key, $"Vui lòng nhập {field.Label.ToLower()}.");
                else if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                         || value < field.Min || value > field.Max)
                    ModelState.AddModelError(key, $"{field.Label} phải là số từ {field.Min} đến {field.Max}.");
            }
        }

        if (!ModelState.IsValid) return View(page);

        // TODO (S3-03): lưu hồ sơ vào database.
        // TODO (S3-04): sinh mã hồ sơ duy nhất, đặt trạng thái ban đầu "Chờ tiếp nhận" (ApplicationStatusEnum.Pending).
        TempData["FormOk"] = "Thông tin hợp lệ. Chức năng lưu hồ sơ sẽ được nối khi S3-03 và S3-04 hoàn tất.";
        return RedirectToAction(nameof(Create));
    }

    // Lấy dữ liệu để vẽ form: hồ sơ cá nhân, đợt đang mở, ngành + phương thức hợp lệ
    private async Task<ApplicationFormPage?> LoadPageAsync(ApplicationFormInput input, CancellationToken ct)
    {
        var userId = users.GetUserId(User);
        if (userId is null) return null;

        var profile = await db.CandidateProfiles.AsNoTracking().Include(x => x.Education)
            .SingleOrDefaultAsync(x => x.UserId == userId, ct);

        // Đợt đang mở: còn sử dụng và thời điểm hiện tại nằm trong [StartAt, EndAt)
        var now = AdmissionClock.Now;
        var round = await db.AdmissionRounds.AsNoTracking()
            .Where(x => x.IsActive && x.StartAt <= now && now < x.EndAt)
            .OrderBy(x => x.EndAt)
            .FirstOrDefaultAsync(ct);

        var majors = new List<FormMajor>();
        if (round is not null)
        {
            majors = await db.RoundMajors.AsNoTracking()
                .Where(x => x.RoundId == round.Id && x.IsEnabled && x.Major!.IsActive)
                .OrderBy(x => x.Major!.Code)
                .Select(x => new FormMajor
                {
                    RoundMajorId = x.Id,
                    Code = x.Major!.Code,
                    Name = x.Major.Name,
                    Methods = x.RoundMajorMethods
                        .Where(l => l.IsEnabled && l.Method!.IsActive)
                        .OrderBy(l => l.Method!.Code)
                        .Select(l => new ApplyMethod
                        {
                            RoundMajorMethodId = l.Id,
                            Code = l.Method!.Code,
                            Name = l.Method.Name,
                            Requirements = l.Method.RequirementsDescription
                        }).ToList()
                }).ToListAsync(ct);
            majors = majors.Where(m => m.Methods.Count > 0).ToList(); // bỏ ngành chưa có phương thức
        }

        return new ApplicationFormPage { Profile = profile, Round = round, Majors = majors, Input = input };
    }
}
