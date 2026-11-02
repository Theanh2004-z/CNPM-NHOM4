using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Data;
using TuyenSinhOnline.Models;
using TuyenSinhOnline.Security;

namespace TuyenSinhOnline.Controllers;

[Authorize(Roles = RoleNames.AdmissionsOfficer)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class AdmissionsController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(new OfficerHome
        {
            FullName = User.FindFirstValue(ClaimTypes.GivenName) ?? User.Identity!.Name!,
            Email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity!.Name!,
            Rounds = await db.AdmissionRounds.AsNoTracking().AsSplitQuery().Where(x => x.IsActive)
                .OrderByDescending(x => x.AdmissionYear).ThenBy(x => x.StartAt).Select(x => new OfficerRound
                {
                    Round = x,
                    Majors = x.RoundMajors.Where(config => config.IsEnabled && config.Major!.IsActive
                            && config.RoundMajorMethods.Any(link => link.IsEnabled && link.Method!.IsActive))
                        .OrderBy(config => config.Major!.Code).Select(config => new OfficerMajor
                        {
                            Code = config.Major!.Code, Name = config.Major.Name, Quota = config.Quota,
                            Methods = config.RoundMajorMethods.Where(link => link.IsEnabled && link.Method!.IsActive)
                                .OrderBy(link => link.Method!.Code).Select(link => link.Method!.Name).ToList()
                        }).ToList()
                }).ToListAsync()
        });
    }

    public async Task<IActionResult> Methods()
    {
        var data = await db.AdmissionMethods.ToListAsync();
        return View(data);
    }

    public async Task<IActionResult> Periods()
    {
        var data = await db.AdmissionRounds.ToListAsync();
        return View(data);
    }
}
