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
        Description = "Quản lý danh mục và kế hoạch tuyển sinh: ngành học, phương thức, thời gian, chỉ tiêu và cấu hình áp dụng.",
        RoleLabel = "Quản trị viên", UserFullName = User.FindFirstValue(ClaimTypes.GivenName) ?? User.Identity!.Name!,
        Links =
        [
            new() { Title = "Ngành học", Description = "Thêm, sửa, tìm kiếm và ẩn ngành học.", Url = "/Majors" },
            new() { Title = "Phương thức xét tuyển", Description = "Quản lý phương thức và điều kiện xét tuyển.", Url = "/AdmissionMethods" },
            new() { Title = "Đợt tuyển sinh", Description = "Cấu hình thời gian mở, đóng và theo dõi trạng thái các đợt.", Url = "/AdmissionRounds" },
            new() { Title = "Chỉ tiêu và phương thức áp dụng", Description = "Gắn ngành với đợt tuyển sinh, chỉ tiêu và các phương thức được áp dụng.", Url = "/Quotas" },
            new() { Title = "Quản lý tài khoản", Description = "Giao diện quản lý tài khoản sẽ được bổ sung theo kế hoạch." },
        ]
    });
}
