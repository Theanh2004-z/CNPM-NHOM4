using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuyenSinhOnline.Security;

namespace TuyenSinhOnline.Controllers;

[Authorize]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class DashboardController : Controller
{
    public IActionResult Index()
    {
        if (User.IsInRole(RoleNames.Administrator)) return RedirectToAction("Index", "Admin");
        if (User.IsInRole(RoleNames.AdmissionsOfficer)) return RedirectToAction("Index", "Admissions");
        if (User.IsInRole(RoleNames.Candidate)) return RedirectToAction("Index", "Candidate");
        return RedirectToAction("AccessDenied", "Account");
    }
}
