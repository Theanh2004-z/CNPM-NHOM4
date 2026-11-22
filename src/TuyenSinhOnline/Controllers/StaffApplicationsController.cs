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

    // ----- S4-05, S4-06: Thống kê tuyển sinh (Cán bộ tuyển sinh & Quản trị viên) -----
    [HttpGet]
    public async Task<IActionResult> Statistics(int? roundId, CancellationToken ct = default)
    {
        var rounds = await db.AdmissionRounds.AsNoTracking()
            .OrderByDescending(r => r.AdmissionYear)
            .ThenByDescending(r => r.StartAt)
            .ToListAsync(ct);

        var query = db.AdmissionApplications.AsNoTracking().AsQueryable();
        if (roundId.HasValue)
        {
            query = query.Where(a => a.RoundMajorMethod!.RoundMajor!.RoundId == roundId.Value);
        }

        // S4-05: Tổng số hồ sơ - truy vấn trực tiếp trên AdmissionApplications (tuyệt đối không JOIN bảng lịch sử)
        var total = await query.CountAsync(ct);

        // 1. Thống kê theo Ngành
        var majorGroup = await query
            .GroupBy(a => new
            {
                Code = a.RoundMajorMethod!.RoundMajor!.Major!.Code,
                Name = a.RoundMajorMethod.RoundMajor.Major.Name
            })
            .Select(g => new
            {
                g.Key.Code,
                g.Key.Name,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Code)
            .ToListAsync(ct);

        var majorRows = majorGroup.Select(x => new StatisticRow
        {
            Code = x.Code,
            Name = x.Name,
            Count = x.Count,
            Percentage = total > 0 ? Math.Round((double)x.Count / total * 100.0, 1) : 0
        }).ToList();

        // 2. Thống kê theo Phương thức xét tuyển
        var methodGroup = await query
            .GroupBy(a => new
            {
                Code = a.RoundMajorMethod!.Method!.Code,
                Name = a.RoundMajorMethod.Method.Name
            })
            .Select(g => new
            {
                g.Key.Code,
                g.Key.Name,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Code)
            .ToListAsync(ct);

        var methodRows = methodGroup.Select(x => new StatisticRow
        {
            Code = x.Code,
            Name = x.Name,
            Count = x.Count,
            Percentage = total > 0 ? Math.Round((double)x.Count / total * 100.0, 1) : 0
        }).ToList();

        // 3. Thống kê theo Trạng thái (lấy đủ 5 trạng thái gốc từ db.ApplicationStatuses, không bị mất dòng)
        var allStatuses = await db.ApplicationStatuses.AsNoTracking()
            .OrderBy(s => s.Id)
            .ToListAsync(ct);

        var statusCounts = await query
            .GroupBy(a => a.StatusId)
            .Select(g => new { StatusId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StatusId, x => x.Count, ct);

        string BadgeClass(byte id) => id switch
        {
            1 => "st-pending",
            2 => "st-review",
            3 => "st-supplement",
            4 => "st-approved",
            5 => "st-rejected",
            _ => "st-pending"
        };

        var statusRows = allStatuses.Select(s =>
        {
            var count = statusCounts.GetValueOrDefault(s.Id, 0);
            return new StatusStatisticRow
            {
                StatusId = s.Id,
                StatusCode = s.Code,
                StatusName = s.Name,
                Count = count,
                Percentage = total > 0 ? Math.Round((double)count / total * 100.0, 1) : 0,
                BadgeClass = BadgeClass(s.Id)
            };
        }).ToList();

        return View(new StaffStatisticsViewModel
        {
            RoundId = roundId,
            Rounds = rounds,
            TotalApplications = total,
            MajorRows = majorRows,
            MethodRows = methodRows,
            StatusRows = statusRows
        });
    }
}