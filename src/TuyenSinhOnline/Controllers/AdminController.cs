using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Data;
using TuyenSinhOnline.Models;
using TuyenSinhOnline.Models.Management;
using TuyenSinhOnline.Security;

namespace TuyenSinhOnline.Controllers;

[Authorize(Roles = RoleNames.Administrator)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class AdminController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var now = AdmissionClock.Now;
        return View(new AdminHome
        {
            FullName = User.FindFirstValue(ClaimTypes.GivenName) ?? User.Identity!.Name!,
            Email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity!.Name!,
            Majors = new(await db.Majors.CountAsync(), await db.Majors.CountAsync(x => x.IsActive)),
            Methods = new(await db.AdmissionMethods.CountAsync(), await db.AdmissionMethods.CountAsync(x => x.IsActive)),
            Rounds = new(await db.AdmissionRounds.CountAsync(), await db.AdmissionRounds.CountAsync(x => x.IsActive)),
            Configurations = new(await db.RoundMajors.CountAsync(), await db.RoundMajors.CountAsync(x => x.IsEnabled)),
            OpenRounds = await db.AdmissionRounds.CountAsync(x => x.IsActive && x.StartAt <= now && now < x.EndAt),
            ConfigurationsToReview = await db.RoundMajors.CountAsync(x => x.IsEnabled &&
                (!x.Major!.IsActive || !x.Round!.IsActive || !x.RoundMajorMethods.Any(link => link.IsEnabled && link.Method!.IsActive))),
            RecentRounds = await db.AdmissionRounds.AsNoTracking().OrderByDescending(x => x.AdmissionYear)
                .ThenByDescending(x => x.StartAt).ThenByDescending(x => x.Id).Take(8)
                .Select(x => new AdminRound { Round = x, Configurations = x.RoundMajors.Count }).ToListAsync()
        });
    }
}
