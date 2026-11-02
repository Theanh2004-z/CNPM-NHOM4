using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Data;
using TuyenSinhOnline.Models;
using TuyenSinhOnline.Models.Management;

namespace TuyenSinhOnline.Controllers;

public class AdmissionMethodsController(ApplicationDbContext db) : ManagementControllerBase
{
    [HttpGet] public async Task<IActionResult> Index(string? q, string? status)
    {
        var query = db.AdmissionMethods.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q)) { q = q.Trim(); query = query.Where(x => x.Code.Contains(q) || x.Name.Contains(q)); }
        if (status == "active") query = query.Where(x => x.IsActive);
        if (status == "inactive") query = query.Where(x => !x.IsActive);
        ViewData["Query"] = q; ViewData["Status"] = status;
        return View(await query.OrderBy(x => x.Code).ToListAsync());
    }
    [HttpGet] public IActionResult Create() => View("Form", new MethodForm());
    [HttpGet] public async Task<IActionResult> Edit(int id)
    {
        var row = await db.AdmissionMethods.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (row is null) return NotFound();
        return View("Form", new MethodForm { Id = row.Id, Code = row.Code, Name = row.Name, RequirementsDescription = row.RequirementsDescription, IsActive = row.IsActive });
    }
    [HttpPost] public Task<IActionResult> Create(MethodForm form) => Save(form, null);
    [HttpPost] public Task<IActionResult> Edit(int id, MethodForm form) => Save(form, id);
    private async Task<IActionResult> Save(MethodForm form, int? id)
    {
        if (id.HasValue && id != form.Id) return BadRequest();
        var row = id.HasValue ? await db.AdmissionMethods.FindAsync(id.Value) : new AdmissionMethod();
        if (row is null) return NotFound();
        if (await db.AdmissionMethods.AnyAsync(x => x.Code == form.Code && x.Id != (id ?? 0)))
            ModelState.AddModelError(nameof(form.Code), "Mã phương thức đã tồn tại.");
        if (!ModelState.IsValid) return View("Form", form);
        row.Code = form.Code; row.Name = form.Name; row.RequirementsDescription = form.RequirementsDescription?.Trim(); row.IsActive = form.IsActive;
        if (!id.HasValue) db.AdmissionMethods.Add(row);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (IsDuplicate(ex)) { ModelState.AddModelError(nameof(form.Code), "Mã phương thức đã tồn tại."); return View("Form", form); }
        Saved("Đã lưu phương thức xét tuyển."); return RedirectToAction(nameof(Index));
    }
    [HttpPost] public async Task<IActionResult> SetActive(int id, bool isActive)
    {
        var row = await db.AdmissionMethods.FindAsync(id);
        if (row is null) return NotFound();
        row.IsActive = isActive; await db.SaveChangesAsync();
        Saved(isActive ? "Đã sử dụng lại phương thức." : "Đã ngừng sử dụng; dữ liệu liên kết được giữ nguyên.");
        return RedirectToAction(nameof(Index));
    }
}
