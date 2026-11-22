using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Data;
using TuyenSinhOnline.Models;
using TuyenSinhOnline.Security;

namespace TuyenSinhOnline.Controllers;

// S4-02: cán bộ và quản trị viên xem danh sách, tìm kiếm, lọc, mở chi tiết hồ sơ.
// S4-03: chỉ cán bộ tuyển sinh được đổi trạng thái hồ sơ.
// Thí sinh (hoặc người chưa đăng nhập) không vào được: bị chuyển sang trang từ chối / đăng nhập.
[Authorize(Roles = $"{RoleNames.AdmissionsOfficer},{RoleNames.Administrator}")]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class StaffApplicationsController(ApplicationDbContext db) : Controller
{
    private const int PageSize = 15;

    // ----- S4-02: danh sách + tìm kiếm + lọc -----
    [HttpGet]
    public async Task<IActionResult> Index(string? q, int? majorId, byte? statusId, int page = 1, CancellationToken ct = default)
    {
        var search = (q ?? string.Empty).Trim();
        if (search.Length > 100) search = search[..100];

        var query = db.AdmissionApplications.AsNoTracking().AsQueryable();
        if (search.Length > 0)
            query = query.Where(a => a.ApplicationCode.Contains(search) || a.FullName.Contains(search));
        if (majorId.HasValue)
            query = query.Where(a => a.RoundMajorMethod!.RoundMajor!.MajorId == majorId.Value);
        if (statusId.HasValue)
            query = query.Where(a => a.StatusId == statusId.Value);

        var total = await query.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var items = await query
            .OrderByDescending(a => a.SubmittedAt).ThenByDescending(a => a.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(a => new StaffApplicationRow
            {
                Id = a.Id,
                ApplicationCode = a.ApplicationCode,
                FullName = a.FullName,
                MajorCode = a.RoundMajorMethod!.RoundMajor!.Major!.Code,
                MajorName = a.RoundMajorMethod.RoundMajor.Major.Name,
                MethodName = a.RoundMajorMethod.Method!.Name,
                RoundName = a.RoundMajorMethod.RoundMajor.Round!.Name,
                StatusId = a.StatusId,
                StatusName = a.Status!.Name,
                SubmittedAt = a.SubmittedAt
            }).ToListAsync(ct);

        return View(new StaffApplicationListViewModel
        {
            Items = items,
            Majors = await db.Majors.AsNoTracking().OrderBy(m => m.Code).ToListAsync(ct),
            Statuses = await db.ApplicationStatuses.AsNoTracking().OrderBy(s => s.Id).ToListAsync(ct),
            Search = search,
            MajorId = majorId,
            StatusId = statusId,
            Page = page,
            TotalPages = totalPages,
            TotalCount = total
        });
    }

    // ----- S4-02: chi tiết hồ sơ (kèm lịch sử và các trạng thái được phép chuyển) -----
    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct = default)
    {
        var application = await db.AdmissionApplications.AsNoTracking().AsSplitQuery()
            .Include(a => a.Status)
            .Include(a => a.Scores)
            .Include(a => a.RoundMajorMethod).ThenInclude(l => l!.Method)
            .Include(a => a.RoundMajorMethod).ThenInclude(l => l!.RoundMajor).ThenInclude(r => r!.Round)
            .Include(a => a.RoundMajorMethod).ThenInclude(l => l!.RoundMajor).ThenInclude(r => r!.Major)
            .SingleOrDefaultAsync(a => a.Id == id, ct);
        if (application is null) return NotFound();

        var history = await db.ApplicationStatusHistories.AsNoTracking()
            .Where(h => h.ApplicationId == id)
            .OrderBy(h => h.ChangedAt).ThenBy(h => h.Id)
            .Select(h => new StaffHistoryRow
            {
                FromStatus = h.FromStatus!.Name,
                ToStatus = h.ToStatus!.Name,
                ChangedAt = h.ChangedAt,
                Note = h.Note,
                ChangedBy = db.Users.Where(u => u.Id == h.ChangedByUserId).Select(u => u.FullName).FirstOrDefault() ?? "—"
            }).ToListAsync(ct);

        var transitions = await db.ApplicationStatusTransitions.AsNoTracking()
            .Where(t => t.FromStatusId == application.StatusId)
            .OrderBy(t => t.ToStatusId)
            .Select(t => new StaffTransitionOption
            {
                ToStatusId = t.ToStatusId,
                ToStatusName = t.ToStatus!.Name,
                RequiresNote = t.RequiresNote
            }).ToListAsync(ct);

        return View(new StaffApplicationDetailViewModel
        {
            Application = application,
            History = history,
            Transitions = transitions,
            CanProcess = User.IsInRole(RoleNames.AdmissionsOfficer)
        });
    }

    // ----- S4-03: đổi trạng thái (chỉ cán bộ tuyển sinh) -----
    // expectedStatusId = trạng thái cán bộ nhìn thấy lúc mở trang; nếu đã đổi thì từ chối (hồ sơ đã được người khác xử lý).
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = RoleNames.AdmissionsOfficer)]
    public async Task<IActionResult> ChangeStatus(int id, byte toStatusId, byte expectedStatusId, string? note, CancellationToken ct = default)
    {
        var officerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (officerId is null) return Challenge();

        if (!Enum.IsDefined(typeof(ApplicationStatusEnum), toStatusId))
        {
            TempData["StaffError"] = "Trạng thái được chọn không hợp lệ.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var (ok, error) = await ApplicationWorkflow.ChangeStatusAsync(
            db, id, (ApplicationStatusEnum)toStatusId, officerId, note, expectedStatusId, ct);

        if (ok) TempData["StaffSuccess"] = "Đã cập nhật trạng thái hồ sơ.";
        else TempData["StaffError"] = error;
        return RedirectToAction(nameof(Details), new { id });
    }
}