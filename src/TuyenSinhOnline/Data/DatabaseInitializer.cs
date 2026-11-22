using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Models;
using TuyenSinhOnline.Security;

namespace TuyenSinhOnline.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration,
        IHostEnvironment environment, bool applyMigrations)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        if (applyMigrations) await db.Database.MigrateAsync();
        await EnsureStatusesAsync(db); // S3-01: bảo đảm có các trạng thái hồ sơ (chỉ thêm dòng còn thiếu)
        await EnsureStatusTransitionsAsync(db); // S4-01: bảo đảm có các chuyển trạng thái hợp lệ
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                EnsureSuccess(await roleManager.CreateAsync(new IdentityRole(role)));
        }
        if (environment.IsDevelopment() && configuration.GetValue<bool>("DemoData:Enabled"))
            await Sprint1DemoData.SeedAsync(db);
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("DemoAccounts:Enabled")) return;
        var password = configuration["DemoAccounts:Password"];
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "Cần đặt DemoAccounts:Password bằng User Secrets hoặc biến môi trường để tạo tài khoản demo.");
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var accounts = new[]
        {
            (Email: "thisinh.demo@tuyensinh.local", Name: "Thí sinh Demo", Role: RoleNames.Candidate),
            (Email: "thisinh2.demo@tuyensinh.local", Name: "Thí sinh Chưa Có Hồ Sơ", Role: RoleNames.Candidate),
            (Email: "canbo.demo@tuyensinh.local", Name: "Cán bộ Demo", Role: RoleNames.AdmissionsOfficer),
            (Email: "admin.demo@tuyensinh.local", Name: "Quản trị viên Demo", Role: RoleNames.Administrator)
        };
        foreach (var account in accounts)
        {
            var user = await userManager.FindByEmailAsync(account.Email);
            if (user is not null) continue; // Không đổi mật khẩu hoặc nâng quyền tài khoản đã tồn tại.
            await using var transaction = await db.Database.BeginTransactionAsync();
            user = new ApplicationUser
            {
                UserName = account.Email,
                Email = account.Email,
                FullName = account.Name,
                EmailConfirmed = false
            };
            EnsureSuccess(await userManager.CreateAsync(user, password));
            EnsureSuccess(await userManager.AddToRoleAsync(user, account.Role));
            await transaction.CommitAsync();
        }
        // S4: Dữ liệu mẫu hoàn chỉnh cho Sprint 4 (chỉ chạy khi bật DemoData và đã có tài khoản demo)
        if (configuration.GetValue<bool>("DemoData:Enabled"))
            await Sprint4DemoData.SeedAsync(db);
    }
    // S4-01: thêm các chuyển trạng thái còn thiếu; không sửa hay xóa dòng đã có.
    private static async Task EnsureStatusTransitionsAsync(ApplicationDbContext db)
    {
        foreach (var rule in ApplicationWorkflowRules.Transitions)
        {
            var from = (byte)rule.From;
            var to = (byte)rule.To;
            var exists = await db.ApplicationStatusTransitions.AnyAsync(t => t.FromStatusId == from && t.ToStatusId == to);
            if (!exists)
                db.ApplicationStatusTransitions.Add(new ApplicationStatusTransition
                {
                    FromStatusId = from,
                    ToStatusId = to,
                    RequiresNote = rule.RequiresNote
                });
        }
        await db.SaveChangesAsync();
    }

    // Thêm các trạng thái hồ sơ còn thiếu; không sửa hay xóa dòng đã có (nhóm có thể đã nhập tay).
    private static async Task EnsureStatusesAsync(ApplicationDbContext db)
    {
        var wanted = new[]
        {
            (Id: (byte)1, Code: "Pending", Name: "Chờ tiếp nhận"),
            (Id: (byte)2, Code: "InReview", Name: "Đang xét duyệt"),
            (Id: (byte)3, Code: "NeedsSupplement", Name: "Cần bổ sung"),
            (Id: (byte)4, Code: "Approved", Name: "Đã duyệt"),
            (Id: (byte)5, Code: "Rejected", Name: "Từ chối")
        };
        foreach (var item in wanted)
        {
            var exists = await db.ApplicationStatuses.AnyAsync(s => s.Id == item.Id || s.Code == item.Code);
            if (!exists)
                db.ApplicationStatuses.Add(new ApplicationStatus { Id = item.Id, Code = item.Code, Name = item.Name });
        }
        await db.SaveChangesAsync();
    }
    private static void EnsureSuccess(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(
                "Không thể khởi tạo tài khoản/vai trò: " + string.Join("; ", result.Errors.Select(e => e.Code)));
    }
}