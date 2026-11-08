using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Data;
using TuyenSinhOnline.Models;
using TuyenSinhOnline.Security;

namespace TuyenSinhOnline.Controllers;

[Authorize(Roles = RoleNames.Candidate)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class CandidateProfileController(ApplicationDbContext db, UserManager<ApplicationUser> users) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = users.GetUserId(User);
        if (userId is null) return Challenge();

        var profile = await db.CandidateProfiles.AsNoTracking().Include(x => x.Education)
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        return profile is null ? await NewFormAsync(userId) : View(profile);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(CancellationToken cancellationToken)
    {
        var userId = users.GetUserId(User);
        if (userId is null) return Challenge();

        var profile = await db.CandidateProfiles.AsNoTracking().Include(x => x.Education)
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (profile is null) return await NewFormAsync(userId);

        return View(new CandidateProfileForm
        {
            FullName = profile.FullName,
            DateOfBirth = profile.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            PhoneNumber = profile.PhoneNumber,
            ContactEmail = profile.ContactEmail,
            SchoolName = profile.Education?.SchoolName,
            GraduationYear = profile.Education?.GraduationYear.ToString("0000", CultureInfo.InvariantCulture)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(CandidateProfileForm form, CancellationToken cancellationToken)
    {
        var userId = users.GetUserId(User);
        if (userId is null) return Challenge();

        var profile = await db.CandidateProfiles.Include(x => x.Education)
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        ViewData["IsNew"] = profile is null;
        if (!ModelState.IsValid) return View(form);

        if (profile is null)
        {
            if (await users.FindByIdAsync(userId) is null) return Challenge();
            profile = new CandidateProfile { UserId = userId };
            db.CandidateProfiles.Add(profile);
        }

        var addingEducation = profile.Education is null;
        profile.FullName = form.FullName!.Trim();
        profile.DateOfBirth = DateOnly.ParseExact(form.DateOfBirth!.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture);
        profile.PhoneNumber = CandidateProfileForm.NormalizePhoneNumber(form.PhoneNumber!.Trim());
        profile.ContactEmail = form.ContactEmail!.Trim();
        profile.UpdatedAt = DateTime.UtcNow;
        profile.Education ??= new CandidateEducation();
        profile.Education.SchoolName = form.SchoolName!.Trim();
        profile.Education.GraduationYear = int.Parse(form.GraduationYear!.Trim(), CultureInfo.InvariantCulture);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (addingEducation
            && exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            ModelState.AddModelError(string.Empty, "Hồ sơ vừa được lưu ở một yêu cầu khác. Vui lòng tải lại trang trước khi sửa.");
            Response.StatusCode = StatusCodes.Status409Conflict;
            return View(form);
        }

        TempData["ProfileSaved"] = "Đã lưu hồ sơ cá nhân.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> NewFormAsync(string userId)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null) return Challenge();

        ViewData["IsNew"] = true;
        return View(nameof(Edit), new CandidateProfileForm { FullName = user.FullName, ContactEmail = user.Email });
    }
}
