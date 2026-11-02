using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Data;
using TuyenSinhOnline.Models;
using TuyenSinhOnline.Models.Management;

namespace TuyenSinhOnline.Controllers;

public class QuotasController(ApplicationDbContext db) : ManagementControllerBase
{
    [HttpGet] public async Task<IActionResult> Index(int? roundId)
    {
        ViewBag.Rounds = await db.AdmissionRounds.AsNoTracking().OrderByDescending(x => x.AdmissionYear).ThenBy(x => x.Code)
            .Select(x => new SelectListItem(x.Code + " — " + x.Name, x.Id.ToString())).ToListAsync();
        ViewData["RoundId"] = roundId;
        var query = db.RoundMajors.AsNoTracking();
        if (roundId.HasValue) query = query.Where(x => x.RoundId == roundId);
        return View(await query.Include(x => x.Round).Include(x => x.Major)
            .Include(x => x.RoundMajorMethods).ThenInclude(x => x.Method)
            .OrderByDescending(x => x.Round!.AdmissionYear).ThenBy(x => x.Round!.Code).ThenBy(x => x.Major!.Code).ToListAsync());
    }
    [HttpGet] public async Task<IActionResult> Create(int? roundId)
    {
        var form = new QuotaForm { RoundId = roundId ?? 0 }; await Populate(form); return View("Form", form);
    }
    [HttpGet] public async Task<IActionResult> Edit(int id)
    {
        var row = await db.RoundMajors.AsNoTracking().Include(x => x.RoundMajorMethods).SingleOrDefaultAsync(x => x.Id == id);
        if (row is null) return NotFound();
        var form = new QuotaForm { Id = row.Id, RoundId = row.RoundId, MajorId = row.MajorId, Quota = row.Quota, IsEnabled = row.IsEnabled,
            SelectedMethodIds = row.RoundMajorMethods.Where(x => x.IsEnabled).Select(x => x.MethodId).ToList() };
        await Populate(form); return View("Form", form);
    }
    [HttpPost] public Task<IActionResult> Create(QuotaForm form) => Save(form, null);
    [HttpPost] public Task<IActionResult> Edit(int id, QuotaForm form) => Save(form, id);
    private async Task<IActionResult> Save(QuotaForm form, int? id)
    {
        if (id.HasValue && id != form.Id) return BadRequest();
        var row = id.HasValue ? await db.RoundMajors.Include(x => x.RoundMajorMethods).SingleOrDefaultAsync(x => x.Id == id) : new RoundMajor();
        if (row is null) return NotFound();
        if (id.HasValue && (row.RoundId != form.RoundId || row.MajorId != form.MajorId))
            ModelState.AddModelError("", "Không thể chuyển cấu hình sang ngành hoặc đợt khác. Hãy tạo cấu hình mới để giữ lịch sử.");

        var round = await db.AdmissionRounds.FindAsync(form.RoundId);
        var major = await db.Majors.FindAsync(form.MajorId);
        if (round is null || ((!id.HasValue || form.IsEnabled) && !round.IsActive))
            ModelState.AddModelError(nameof(form.RoundId), "Đợt tuyển sinh không tồn tại hoặc đã ngừng sử dụng.");
        if (major is null || ((!id.HasValue || form.IsEnabled) && !major.IsActive))
            ModelState.AddModelError(nameof(form.MajorId), "Ngành học không tồn tại hoặc đã ẩn.");
        if (await db.RoundMajors.AnyAsync(x => x.RoundId == form.RoundId && x.MajorId == form.MajorId && x.Id != (id ?? 0)))
            ModelState.AddModelError("", "Ngành này đã có cấu hình trong đợt. Hãy sửa bản ghi hiện có.");

        var selected = (form.SelectedMethodIds ?? []).Distinct().ToList();
        var methods = await db.AdmissionMethods.Where(x => selected.Contains(x.Id)).ToListAsync();
        var prior = row.RoundMajorMethods.Where(x => x.IsEnabled).Select(x => x.MethodId).ToHashSet();
        if (methods.Count != selected.Count || methods.Any(x => !x.IsActive && !prior.Contains(x.Id)))
            ModelState.AddModelError(nameof(form.SelectedMethodIds), "Phương thức không tồn tại hoặc đã ngừng sử dụng.");
        if (form.IsEnabled && selected.Count == 0)
            ModelState.AddModelError(nameof(form.SelectedMethodIds), "Chọn ít nhất một phương thức cho cấu hình đang sử dụng.");
        if (!ModelState.IsValid) { await Populate(form); return View("Form", form); }

        row.RoundId = form.RoundId; row.MajorId = form.MajorId; row.Quota = form.Quota; row.IsEnabled = form.IsEnabled;
        // Giữ ID liên kết cũ để hồ sơ các Sprint sau không bị mất tham chiếu.
        foreach (var link in row.RoundMajorMethods) link.IsEnabled = selected.Contains(link.MethodId);
        foreach (var methodId in selected.Where(methodId => row.RoundMajorMethods.All(x => x.MethodId != methodId)))
            row.RoundMajorMethods.Add(new RoundMajorMethod { MethodId = methodId, IsEnabled = true });
        if (!id.HasValue) db.RoundMajors.Add(row);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (IsDuplicate(ex))
        {
            ModelState.AddModelError("", "Cấu hình ngành và đợt đã tồn tại. Vui lòng tải lại danh sách.");
            await Populate(form); return View("Form", form);
        }
        Saved("Đã lưu chỉ tiêu và phương thức áp dụng."); return RedirectToAction(nameof(Index), new { roundId = form.RoundId });
    }
    private async Task Populate(QuotaForm form)
    {
        form.Rounds = await db.AdmissionRounds.AsNoTracking().Where(x => x.IsActive || x.Id == form.RoundId)
            .OrderByDescending(x => x.AdmissionYear).ThenBy(x => x.Code)
            .Select(x => new SelectListItem(x.Code + " — " + x.Name + (x.IsActive ? "" : " (ngừng sử dụng)"), x.Id.ToString())).ToListAsync();
        form.Majors = await db.Majors.AsNoTracking().Where(x => x.IsActive || x.Id == form.MajorId).OrderBy(x => x.Code)
            .Select(x => new SelectListItem(x.Code + " — " + x.Name + (x.IsActive ? "" : " (đã ẩn)"), x.Id.ToString())).ToListAsync();
        var priorIds = form.Id == 0 ? [] : await db.RoundMajorMethods.AsNoTracking().Where(x => x.RoundMajorId == form.Id && x.IsEnabled).Select(x => x.MethodId).ToListAsync();
        form.Methods = await db.AdmissionMethods.AsNoTracking().Where(x => x.IsActive || priorIds.Contains(x.Id)).OrderBy(x => x.Code)
            .Select(x => new MethodChoice(x.Id, x.Code, x.Name, x.IsActive)).ToListAsync();
    }
}
