using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuyenSinhOnline.Models;
using TuyenSinhOnline.Security;

namespace TuyenSinhOnline.Controllers;

[Authorize(Roles = RoleNames.Administrator)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class AdminController : Controller
{
    public IActionResult Index() => View("~/Views/Dashboard/Workspace.cshtml", new WorkspaceViewModel
    {
        Title = "Không gian quản trị", Eyebrow = "QUẢN TRỊ HỆ THỐNG",
        Description = "Bạn đã đăng nhập bằng tài khoản quản trị viên. Các chức năng quản trị được phát triển theo từng Sprint.",
        RoleLabel = "Quản trị viên", UserFullName = User.FindFirstValue(ClaimTypes.GivenName) ?? User.Identity!.Name!,
        Links =
        [
            new() { Title = "Trang thông tin chung", Description = "Trở về trang giới thiệu hệ thống.", Url = "/" },
            new() { Title = "Quản lý tài khoản", Description = "Giao diện quản lý tài khoản sẽ được bổ sung theo kế hoạch." },
            new() { Title = "Cấu hình tuyển sinh", Description = "Chờ tích hợp các phần ngành, chỉ tiêu, phương thức và thời gian tuyển sinh." }
        ]
    });
}
