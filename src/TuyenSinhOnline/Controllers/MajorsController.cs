using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Data;
using TuyenSinhOnline.Models;
using TuyenSinhOnline.Models.Management;

namespace TuyenSinhOnline.Controllers;

public class MajorsController(ApplicationDbContext db) : ManagementControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? status)
    {
        var query = db.Majors.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q)) { q = q.Trim(); query = query.Where(x => x.Code.Contains(q) || x.Name.Contains(q)); }
        if (status == "active") query = query.Where(x => x.IsActive);
        if (status == "inactive") query = query.Where(x => !x.IsActive);
        ViewData["Query"] = q; ViewData["Status"] = status;
        return View(await query.OrderBy(x => x.Code).ToListAsync());
    }
    [HttpGet] public IActionResult Create() => View("Form", new MajorForm());
    [HttpGet] public async Task<IActionResult> Edit(int id)
    {
        var row = await db.Majors.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (row is null) return NotFound();
        return View("Form", new MajorForm { Id = row.Id, Code = row.Code, Name = row.Name, Description = row.Description, IsActive = row.IsActive });
    }
    [HttpPost] public Task<IActionResult> Create(MajorForm form) => Save(form, null);
    [HttpPost] public Task<IActionResult> Edit(int id, MajorForm form) => Save(form, id);
    private async Task<IActionResult> Save(MajorForm form, int? id)
    {
        if (id.HasValue && id != form.Id) return BadRequest();
        var row = id.HasValue ? await db.Majors.FindAsync(id.Value) : new Major();
        if (row is null) return NotFound();
        if (await db.Majors.AnyAsync(x => x.Code == form.Code && x.Id != (id ?? 0)))
            ModelState.AddModelError(nameof(form.Code), "Mã ngành đã tồn tại.");
        if (!ModelState.IsValid) return View("Form", form);
        row.Code = form.Code; row.Name = form.Name; row.Description = form.Description?.Trim(); row.IsActive = form.IsActive;
        if (!id.HasValue) db.Majors.Add(row);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (IsDuplicate(ex)) { ModelState.AddModelError(nameof(form.Code), "Mã ngành đã tồn tại."); return View("Form", form); }
        Saved("Đã lưu ngành học."); return RedirectToAction(nameof(Index));
    }
    [HttpPost] public async Task<IActionResult> SetActive(int id, bool isActive)
    {
        var row = await db.Majors.FindAsync(id);
        if (row is null) return NotFound();
        row.IsActive = isActive; await db.SaveChangesAsync();
        Saved(isActive ? "Đã sử dụng lại ngành học." : "Đã ẩn ngành; dữ liệu liên kết được giữ nguyên.");
        return RedirectToAction(nameof(Index));
    }
}
