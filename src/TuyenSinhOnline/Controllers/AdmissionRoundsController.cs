using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Data;
using TuyenSinhOnline.Models;
using TuyenSinhOnline.Models.Management;

namespace TuyenSinhOnline.Controllers;

public class AdmissionRoundsController(ApplicationDbContext db) : ManagementControllerBase
{
    [HttpGet] public async Task<IActionResult> Index(string? q, int? year)
    {
        var query = db.AdmissionRounds.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q)) { q = q.Trim(); query = query.Where(x => x.Code.Contains(q) || x.Name.Contains(q)); }
        if (year.HasValue) query = query.Where(x => x.AdmissionYear == year.Value);
        ViewData["Query"] = q; ViewData["Year"] = year;
        return View(await query.OrderByDescending(x => x.AdmissionYear).ThenByDescending(x => x.StartAt).ToListAsync());
    }
    [HttpGet] public IActionResult Create() => View("Form", new RoundForm());
    [HttpGet] public async Task<IActionResult> Edit(int id)
    {
        var row = await db.AdmissionRounds.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (row is null) return NotFound();
        return View("Form", new RoundForm { Id = row.Id, Code = row.Code, Name = row.Name, AdmissionYear = row.AdmissionYear, StartAt = row.StartAt, EndAt = row.EndAt, IsActive = row.IsActive });
    }
    [HttpGet] public async Task<IActionResult> Details(int id)
    {
        var row = await db.AdmissionRounds.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (row is null) return NotFound();
        return View(new RoundDetails
        {
            Round = row,
            Configurations = await db.RoundMajors.AsNoTracking().Where(x => x.RoundId == id)
                .Include(x => x.Round).Include(x => x.Major).Include(x => x.RoundMajorMethods).ThenInclude(x => x.Method)
                .OrderBy(x => x.Major!.Code).ToListAsync()
        });
    }
    [HttpPost] public Task<IActionResult> Create(RoundForm form) => Save(form, null);
    [HttpPost] public Task<IActionResult> Edit(int id, RoundForm form) => Save(form, id);
    private async Task<IActionResult> Save(RoundForm form, int? id)
    {
        if (id.HasValue && id != form.Id) return BadRequest();
        var row = id.HasValue ? await db.AdmissionRounds.FindAsync(id.Value) : new AdmissionRound();
        if (row is null) return NotFound();
        if (await db.AdmissionRounds.AnyAsync(x => x.Code == form.Code && x.Id != (id ?? 0)))
            ModelState.AddModelError(nameof(form.Code), "Mã đợt tuyển sinh đã tồn tại.");
        if (!ModelState.IsValid) return View("Form", form);
        row.Code = form.Code; row.Name = form.Name; row.AdmissionYear = form.AdmissionYear;
        row.StartAt = DateTime.SpecifyKind(form.StartAt!.Value, DateTimeKind.Unspecified);
        row.EndAt = DateTime.SpecifyKind(form.EndAt!.Value, DateTimeKind.Unspecified); row.IsActive = form.IsActive;
        if (!id.HasValue) db.AdmissionRounds.Add(row);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (IsDuplicate(ex)) { ModelState.AddModelError(nameof(form.Code), "Mã đợt tuyển sinh đã tồn tại."); return View("Form", form); }
        Saved("Đã lưu đợt tuyển sinh và thời gian tiếp nhận."); return RedirectToAction(nameof(Details), new { row.Id });
    }
    [HttpPost] public async Task<IActionResult> SetActive(int id, bool isActive)
    {
        var row = await db.AdmissionRounds.FindAsync(id);
        if (row is null) return NotFound();
        row.IsActive = isActive; await db.SaveChangesAsync();
        Saved(isActive ? "Đã sử dụng lại đợt tuyển sinh." : "Đã ngừng sử dụng đợt; dữ liệu liên quan được giữ nguyên.");
        return RedirectToAction(nameof(Index));
    }
}
