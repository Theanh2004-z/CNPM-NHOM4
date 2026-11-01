using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuyenSinhOnline.Models;
using TuyenSinhOnline.Security;

namespace TuyenSinhOnline.Controllers;

[Authorize(Roles = RoleNames.Candidate)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class CandidateController : Controller
{
    public IActionResult Index() => View("~/Views/Dashboard/Workspace.cshtml", new WorkspaceViewModel
    {
        Title = "Không gian thí sinh", Eyebrow = "HÀNH TRÌNH TUYỂN SINH",
        Description = "Tài khoản thí sinh của bạn đã sẵn sàng. Các chức năng xét tuyển sẽ được bổ sung theo kế hoạch của nhóm.",
        RoleLabel = "Thí sinh", UserFullName = User.FindFirstValue(ClaimTypes.GivenName) ?? User.Identity!.Name!,
        Links =
        [
            new() { Title = "Thông tin tuyển sinh", Description = "Trở về trang giới thiệu hệ thống.", Url = "/" },
            new() { Title = "Hồ sơ xét tuyển", Description = "Tạo hồ sơ và đăng ký nguyện vọng trong Sprint tiếp theo." },
            new() { Title = "Theo dõi kết quả", Description = "Theo dõi trạng thái hồ sơ và kết quả xét tuyển khi có dữ liệu." }
        ]
    });
}
