using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Data;
using TuyenSinhOnline.Models;
using TuyenSinhOnline.Models.Management;
using TuyenSinhOnline.Security;

namespace TuyenSinhOnline.Controllers;

[Authorize(Roles = RoleNames.Candidate)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class CandidateController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? roundId, string? q)
    {
        var now = AdmissionClock.Now;
        var rounds = await db.AdmissionRounds.AsNoTracking().Where(x => x.IsActive)
            .OrderByDescending(x => x.AdmissionYear).ThenBy(x => x.StartAt).ToListAsync();
        var selected = roundId.HasValue
            ? rounds.SingleOrDefault(x => x.Id == roundId.Value)
            : rounds.Where(x => x.StartAt <= now && now < x.EndAt).OrderBy(x => x.EndAt).FirstOrDefault()
                ?? rounds.Where(x => x.StartAt > now).OrderBy(x => x.StartAt).FirstOrDefault()
                ?? rounds.OrderByDescending(x => x.EndAt).FirstOrDefault();
        if (roundId.HasValue && selected is null) return NotFound();

        var search = (q ?? string.Empty).Trim();
        if (search.Length > 100) search = search[..100];
        var majors = new List<CandidateMajor>();
        if (selected is not null)
        {
            // Chỉ đọc cấu hình đang sử dụng; không công khai các liên kết đã tắt.
            var query = db.RoundMajors.AsNoTracking().Where(x => x.RoundId == selected.Id
                && x.IsEnabled && x.Major!.IsActive
                && x.RoundMajorMethods.Any(link => link.IsEnabled && link.Method!.IsActive));
            if (search.Length > 0)
                query = query.Where(x => x.Major!.Code.Contains(search) || x.Major.Name.Contains(search));
            majors = await query.OrderBy(x => x.Major!.Code).Select(x => new CandidateMajor
            {
                Code = x.Major!.Code, Name = x.Major.Name, Description = x.Major.Description,
                Quota = x.Quota,
                Methods = x.RoundMajorMethods.Where(link => link.IsEnabled && link.Method!.IsActive)
                    .OrderBy(link => link.Method!.Code).Select(link => new CandidateMethod
                    {
                        Code = link.Method!.Code, Name = link.Method.Name,
                        Requirements = link.Method.RequirementsDescription
                    }).ToList()
            }).ToListAsync();
        }
        return View(new CandidateHome
        {
            FullName = User.FindFirstValue(ClaimTypes.GivenName) ?? User.Identity!.Name!,
            Email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity!.Name!,
            Rounds = rounds, SelectedRound = selected, Search = search, Majors = majors,
            RoundStatus = selected is null ? string.Empty : AdmissionClock.Status(selected, now)
        });
    }
}
