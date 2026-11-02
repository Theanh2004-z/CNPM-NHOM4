using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Models;
using TuyenSinhOnline.Security;
using TuyenSinhOnline.Data;

namespace TuyenSinhOnline.Controllers;

[Authorize(Roles = RoleNames.AdmissionsOfficer)]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class AdmissionsController : Controller
{
    private readonly ApplicationDbContext _context;

    public AdmissionsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public IActionResult Index() => View("~/Views/Dashboard/Workspace.cshtml", new WorkspaceViewModel
    {
        Title = "Không gian cán bộ",
        Eyebrow = "NGHIỆP VỤ TUYỂN SINH",
        Description = "Bạn đã đăng nhập bằng tài khoản cán bộ tuyển sinh. Khu vực này chỉ dành cho cán bộ được cấp quyền.",
        RoleLabel = "Cán bộ tuyển sinh",
        UserFullName = User.FindFirstValue(ClaimTypes.GivenName) ?? User.Identity!.Name!,
        Links =
        [
            new() { Title = "Trang thông tin chung", Description = "Xem giới thiệu hệ thống tuyển sinh.", Url = "/" },
           new() { Title = "Kiểm tra hồ sơ", Description = "Tiếp nhận và duyệt hồ sơ sẽ được phát triển ở Sprint sau." },
           new() { Title = "Kết quả tuyển sinh", Description = "Nhập và công bố kết quả theo kế hoạch của nhóm." }
        ]
    });

    public async Task<IActionResult> Methods()
    {
        var data = await _context.AdmissionMethods.ToListAsync();
        return View(data);
    }

    // 2. Quản lý đợt và thời gian tuyển sinh  
    public async Task<IActionResult> Periods()
    {
        var data = await _context.AdmissionRounds.ToListAsync();
        return View(data);
    }
}
