using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Data;
using TuyenSinhOnline.Models;
using TuyenSinhOnline.Models.Management;
using TuyenSinhOnline.Security;

namespace TuyenSinhOnline.Controllers;

/// <summary>
/// Quản lý biểu mẫu đăng ký xét tuyển trực tuyến cho thí sinh.
/// Thẻ S3-03: Kiểm tra điều kiện đăng ký và thời gian tiếp nhận phía máy chủ.
/// Thẻ S3-04: Gửi hồ sơ, sinh mã duy nhất, lưu snapshot và trạng thái ban đầu "Chờ tiếp nhận".
/// </summary>
[Authorize(Roles = RoleNames.Candidate)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ApplicationFormController(ApplicationDbContext db, UserManager<ApplicationUser> users) : Controller
{
    // S3-05: Danh sách hồ sơ đã nộp của thí sinh
    [HttpGet]
    public async Task<IActionResult> Index(int? roundId, string? q, CancellationToken ct)
    {
        var userId = users.GetUserId(User);
        if (userId is null) return Challenge();

        var query = db.AdmissionApplications.AsNoTracking()
            .Where(a => a.UserId == userId)
            .Include(a => a.Status)
            .Include(a => a.RoundMajorMethod)
                .ThenInclude(rmm => rmm!.RoundMajor)
                    .ThenInclude(rm => rm!.Major)
            .Include(a => a.RoundMajorMethod)
                .ThenInclude(rmm => rmm!.RoundMajor)
                    .ThenInclude(rm => rm!.Round)
            .Include(a => a.RoundMajorMethod)
                .ThenInclude(rmm => rmm!.Method)
            .AsQueryable();

        var totalCount = await db.AdmissionApplications.CountAsync(a => a.UserId == userId, ct);

        if (roundId.HasValue)
        {
            query = query.Where(a => a.RoundMajorMethod!.RoundMajor!.RoundId == roundId.Value);
        }

        var search = (q ?? string.Empty).Trim();
        if (search.Length > 0)
        {
            query = query.Where(a => a.ApplicationCode.Contains(search)
                || a.RoundMajorMethod!.RoundMajor!.Major!.Code.Contains(search)
                || a.RoundMajorMethod!.RoundMajor!.Major!.Name.Contains(search));
        }

        var applications = await query.OrderByDescending(a => a.SubmittedAt).ToListAsync(ct);

        var rounds = await db.AdmissionRounds.AsNoTracking()
            .Where(r => r.IsActive)
            .OrderByDescending(r => r.AdmissionYear)
            .ThenBy(r => r.StartAt)
            .ToListAsync(ct);

        return View(new ApplicationListViewModel
        {
            Applications = applications,
            Rounds = rounds,
            SelectedRoundId = roundId,
            Search = search,
            TotalCount = totalCount
        });
    }

    // Hiển thị form đăng ký xét tuyển
    [HttpGet]
    public async Task<IActionResult> Create(int? roundId, CancellationToken ct)
    {
        var userId = users.GetUserId(User);
        if (userId is null) return Challenge();

        var profile = await db.CandidateProfiles.AsNoTracking().Include(x => x.Education)
            .SingleOrDefaultAsync(x => x.UserId == userId, ct);

        var page = await BuildPageForRoundAsync(roundId, profile, new ApplicationFormInput(), ct);
        return View(page);
    }

    // Nhận dữ liệu form: kiểm tra lỗi chặt chẽ ở máy chủ (S3-03) và lưu hồ sơ (S3-04)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ApplicationFormInput input, CancellationToken ct)
    {
        // 1. Xác thực & xác định danh tính chủ sở hữu từ Identity Claims (TC01, TC02, TC12)
        var userId = users.GetUserId(User);
        if (userId is null) return Challenge();

        // 2. Kiểm tra hồ sơ cá nhân và học vấn phía máy chủ (TC03, TC04)
        var profile = await db.CandidateProfiles.AsNoTracking().Include(x => x.Education)
            .SingleOrDefaultAsync(x => x.UserId == userId, ct);

        if (profile is null || string.IsNullOrWhiteSpace(profile.FullName) || string.IsNullOrWhiteSpace(profile.PhoneNumber) || string.IsNullOrWhiteSpace(profile.ContactEmail) || profile.DateOfBirth == default)
        {
            ModelState.AddModelError(string.Empty, "Thông tin cá nhân chưa đầy đủ hoặc chưa được tạo. Vui lòng hoàn thiện hồ sơ cá nhân trước khi đăng ký xét tuyển.");
            return View(await BuildPageForRoundAsync(input.RoundId, profile, input, ct));
        }

        if (profile.Education is null || string.IsNullOrWhiteSpace(profile.Education.SchoolName) || profile.Education.GraduationYear <= 0)
        {
            ModelState.AddModelError(string.Empty, "Thông tin học tập cấp THPT chưa đầy đủ. Vui lòng cập nhật trường THPT và năm tốt nghiệp trước khi đăng ký xét tuyển.");
            return View(await BuildPageForRoundAsync(input.RoundId, profile, input, ct));
        }

        // 3. Kiểm tra đợt tuyển sinh và khung thời gian tiếp nhận (TC05, TC06, TC07, TC08)
        var now = AdmissionClock.Now;
        AdmissionRound? round = null;
        if (!input.RoundId.HasValue)
        {
            ModelState.AddModelError("RoundId", "Vui lòng chọn đợt tuyển sinh.");
        }
        else
        {
            round = await db.AdmissionRounds.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.RoundId.Value, ct);
            if (round is null)
            {
                ModelState.AddModelError("RoundId", "Đợt tuyển sinh không tồn tại trong hệ thống.");
            }
            else if (!round.IsActive)
            {
                ModelState.AddModelError("RoundId", "Đợt tuyển sinh đang tạm đóng (ngừng tiếp nhận).");
            }
            else if (now < round.StartAt)
            {
                ModelState.AddModelError("RoundId", $"Đợt tuyển sinh chưa đến thời gian tiếp nhận (bắt đầu lúc {round.StartAt:dd/MM/yyyy HH:mm:ss}).");
            }
            else if (now >= round.EndAt)
            {
                ModelState.AddModelError("RoundId", $"Đợt tuyển sinh đã kết thúc thời gian tiếp nhận (hết hạn lúc {round.EndAt:dd/MM/yyyy HH:mm:ss}).");
            }
        }

        // 4. Kiểm tra dây chuyền hợp lệ: Đợt -> Ngành -> Phương thức (TC09, TC10)
        ApplyMethod? selectedMethod = null;
        RoundMajorMethod? verifiedRoundMajorMethod = null;

        if (round is not null && round.IsActive && round.StartAt <= now && now < round.EndAt)
        {
            if (!input.RoundMajorId.HasValue)
            {
                ModelState.AddModelError("RoundMajorId", "Vui lòng chọn ngành.");
            }
            else
            {
                var roundMajor = await db.RoundMajors.AsNoTracking()
                    .Include(x => x.Major)
                    .SingleOrDefaultAsync(x => x.Id == input.RoundMajorId.Value && x.RoundId == round.Id && x.IsEnabled && x.Major!.IsActive, ct);

                if (roundMajor is null)
                {
                    ModelState.AddModelError("RoundMajorId", "Ngành đã chọn không tồn tại hoặc không mở tiếp nhận trong đợt này.");
                }
                else
                {
                    if (!input.RoundMajorMethodId.HasValue)
                    {
                        ModelState.AddModelError("RoundMajorMethodId", "Vui lòng chọn phương thức áp dụng cho ngành này.");
                    }
                    else
                    {
                        verifiedRoundMajorMethod = await db.RoundMajorMethods.AsNoTracking()
                            .Include(x => x.Method)
                            .SingleOrDefaultAsync(x => x.Id == input.RoundMajorMethodId.Value && x.RoundMajorId == roundMajor.Id && x.IsEnabled && x.Method!.IsActive, ct);

                        if (verifiedRoundMajorMethod is null)
                        {
                            ModelState.AddModelError("RoundMajorMethodId", "Phương thức xét tuyển không hợp lệ hoặc không áp dụng cho ngành này.");
                        }
                        else
                        {
                            selectedMethod = new ApplyMethod
                            {
                                RoundMajorMethodId = verifiedRoundMajorMethod.Id,
                                Code = verifiedRoundMajorMethod.Method!.Code,
                                Name = verifiedRoundMajorMethod.Method.Name,
                                Requirements = verifiedRoundMajorMethod.Method.RequirementsDescription
                            };

                            // 5. Kiểm tra dữ liệu điểm theo phương thức (TC11)
                            var fields = MethodFieldCatalog.For(selectedMethod.Code);
                            foreach (var field in fields)
                            {
                                var text = input.Scores.GetValueOrDefault(field.Key)?.Trim().Replace(',', '.') ?? "";
                                var key = $"Scores[{field.Key}]";
                                if (text.Length == 0)
                                {
                                    ModelState.AddModelError(key, $"Vui lòng nhập {field.Label.ToLower()}.");
                                }
                                else if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                                         || value < field.Min || value > field.Max)
                                {
                                    ModelState.AddModelError(key, $"{field.Label} phải là số từ {field.Min} đến {field.Max}.");
                                }
                            }
                        }
                    }
                }
            }
        }

        // 6. Kiểm tra chống gửi lặp phía Server trước khi vào Transaction (TC19)
        if (verifiedRoundMajorMethod is not null)
        {
            var alreadyApplied = await db.AdmissionApplications.AsNoTracking().AnyAsync(
                a => a.UserId == userId && a.RoundMajorMethodId == verifiedRoundMajorMethod.Id, ct);
            if (alreadyApplied)
            {
                ModelState.AddModelError(string.Empty, "Bạn đã nộp hồ sơ đăng ký cho ngành và phương thức này trong đợt tuyển sinh rồi. Mỗi nguyện vọng chỉ được nộp một lần.");
            }
        }

        if (!ModelState.IsValid)
        {
            var page = await BuildPageForRoundAsync(input.RoundId, profile, input, ct);
            return View(page);
        }

        // ==========================================
        // S3-04: LƯU HỒ SƠ, SINH MÃ VÀ GÁN TRẠNG THÁI
        // ==========================================
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Snapshot thông tin cá nhân và học tập tại thời điểm nộp (TC18)
            var application = new AdmissionApplication
            {
                UserId = userId,
                RoundMajorMethodId = verifiedRoundMajorMethod!.Id,
                StatusId = (byte)ApplicationStatusEnum.Pending, // Trạng thái ban đầu "Chờ tiếp nhận" (TC17)
                SubmittedAt = now,
                FullName = profile.FullName.Trim(),
                DateOfBirth = profile.DateOfBirth,
                PhoneNumber = profile.PhoneNumber.Trim(),
                ContactEmail = profile.ContactEmail.Trim(),
                SchoolName = profile.Education!.SchoolName.Trim(),
                GraduationYear = profile.Education.GraduationYear
            };

            // Snapshot danh sách điểm xét tuyển
            var methodFields = MethodFieldCatalog.For(selectedMethod!.Code);
            foreach (var field in methodFields)
            {
                var text = input.Scores.GetValueOrDefault(field.Key)?.Trim().Replace(',', '.') ?? "0";
                var val = decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture);
                application.Scores.Add(new ApplicationScore
                {
                    FieldKey = field.Key,
                    FieldLabel = field.Label,
                    Value = Math.Round(val, 2, MidpointRounding.AwayFromZero)
                });
            }

            db.AdmissionApplications.Add(application);

            // Sinh mã duy nhất dạng HS{Năm}-{6 số ngẫu nhiên} kèm retry tối đa 5 lần (TC16, TC21)
            const int maxRetries = 5;
            var saved = false;
            var admissionYear = round!.AdmissionYear;

            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                var code = GenerateApplicationCode(admissionYear);
                if (await db.AdmissionApplications.AnyAsync(a => a.ApplicationCode == code, ct))
                {
                    continue;
                }

                application.ApplicationCode = code;

                try
                {
                    await db.SaveChangesAsync(ct);
                    saved = true;
                    break;
                }
                catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
                {
                    // Trùng lặp do nguyện vọng (UserId + RoundMajorMethodId)
                    if (sqlEx.Message.Contains("IX_AdmissionApplications_UserId_RoundMajorMethodId"))
                    {
                        await transaction.RollbackAsync(ct);
                        ModelState.AddModelError(string.Empty, "Bạn đã nộp hồ sơ xét tuyển cho ngành và phương thức này rồi.");
                        var page = await BuildPageForRoundAsync(input.RoundId, profile, input, ct);
                        return View(page);
                    }

                    // Trùng mã ApplicationCode -> thử lại mã ngẫu nhiên mới
                    if (attempt == maxRetries)
                    {
                        throw new InvalidOperationException("Không thể sinh mã hồ sơ duy nhất sau 5 lần thử.", ex);
                    }
                }
            }

            if (!saved)
            {
                await transaction.RollbackAsync(ct);
                ModelState.AddModelError(string.Empty, "Không thể lưu hồ sơ vào hệ thống. Vui lòng thử lại.");
                var page = await BuildPageForRoundAsync(input.RoundId, profile, input, ct);
                return View(page);
            }

            // Ghi dữ liệu thành công xuống database (TC15, TC22)
            await transaction.CommitAsync(ct);

            TempData["SuccessMessage"] = $"Nộp hồ sơ thành công! Mã hồ sơ của bạn là: {application.ApplicationCode}";
            TempData["ApplicationCode"] = application.ApplicationCode;

            // Mẫu PRG (Post-Redirect-Get) chuyển hướng sang trang chi tiết hồ sơ (TC15, TC20)
            return RedirectToAction(nameof(Details), new { id = application.Id });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(ct);
            ModelState.AddModelError(string.Empty, "Đã xảy ra lỗi trong quá trình lưu hồ sơ. Dữ liệu đã được hủy an toàn. Vui lòng thử lại.");
            var page = await BuildPageForRoundAsync(input.RoundId, profile, input, ct);
            return View(page);
        }
    }

    // S3-05: Xem chi tiết hồ sơ đã nộp và trạng thái (chống IDOR - TC26, TC27, TC28)
    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var userId = users.GetUserId(User);
        if (userId is null) return Challenge();

        var application = await db.AdmissionApplications.AsNoTracking()
            .Include(a => a.Status)
            .Include(a => a.Scores)
            .Include(a => a.RoundMajorMethod)
                .ThenInclude(rmm => rmm!.RoundMajor)
                    .ThenInclude(rm => rm!.Major)
            .Include(a => a.RoundMajorMethod)
                .ThenInclude(rmm => rmm!.RoundMajor)
                    .ThenInclude(rm => rm!.Round)
            .Include(a => a.RoundMajorMethod)
                .ThenInclude(rmm => rmm!.Method)
            .SingleOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);

        if (application is null) return NotFound();

        // S4-04: lịch sử cập nhật trạng thái cho thí sinh xem (không hiện tên cán bộ)
        ViewData["History"] = await db.ApplicationStatusHistories.AsNoTracking()
            .Where(h => h.ApplicationId == application.Id)
            .OrderBy(h => h.ChangedAt).ThenBy(h => h.Id)
            .Select(h => new CandidateHistoryRow
            {
                FromStatus = h.FromStatus!.Name,
                ToStatus = h.ToStatus!.Name,
                ChangedAt = h.ChangedAt,
                Note = h.Note
            }).ToListAsync(ct);

        return View(application);
    }

    private static string GenerateApplicationCode(int admissionYear)
    {
        var randomDigits = Random.Shared.Next(100000, 1000000); // 100000..999999
        return $"HS{admissionYear}-{randomDigits}";
    }

    private async Task<ApplicationFormPage> BuildPageForRoundAsync(
        int? roundId,
        CandidateProfile? profile,
        ApplicationFormInput input,
        CancellationToken ct)
    {
        var now = AdmissionClock.Now;
        AdmissionRound? round = null;

        if (roundId.HasValue)
        {
            round = await db.AdmissionRounds.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == roundId.Value, ct);
        }

        // Nếu không chỉ định hoặc đợt không tìm thấy, lấy đợt đang mở ưu tiên
        round ??= await db.AdmissionRounds.AsNoTracking()
            .Where(x => x.IsActive && x.StartAt <= now && now < x.EndAt)
            .OrderBy(x => x.EndAt)
            .FirstOrDefaultAsync(ct);

        var majors = new List<FormMajor>();
        if (round is not null && round.IsActive && round.StartAt <= now && now < round.EndAt)
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
            majors = majors.Where(m => m.Methods.Count > 0).ToList();
        }

        input.RoundId ??= round?.Id;
        return new ApplicationFormPage { Profile = profile, Round = round, Majors = majors, Input = input };
    }
}