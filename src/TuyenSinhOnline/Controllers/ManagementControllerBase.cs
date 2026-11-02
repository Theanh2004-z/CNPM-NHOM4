using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Security;

namespace TuyenSinhOnline.Controllers;

[Authorize(Roles = RoleNames.Administrator)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public abstract class ManagementControllerBase : Controller
{
    protected static bool IsDuplicate(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
    protected void Saved(string message) => TempData["ManagementSuccess"] = message;
}
