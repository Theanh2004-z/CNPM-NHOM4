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
      //  if (applyMigrations) await db.Database.MigrateAsync();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                EnsureSuccess(await roleManager.CreateAsync(new IdentityRole(role)));
        }
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("DemoAccounts:Enabled")) return;
        var password = configuration["DemoAccounts:Password"];
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "Cần đặt DemoAccounts:Password bằng User Secrets hoặc biến môi trường để tạo tài khoản demo.");
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var accounts = new[]
        {
            (Email: "thisinh.demo@tuyensinh.local", Name: "Thí sinh Demo", Role: RoleNames.Candidate),
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
                UserName = account.Email, Email = account.Email,
                FullName = account.Name, EmailConfirmed = false
            };
            EnsureSuccess(await userManager.CreateAsync(user, password));
            EnsureSuccess(await userManager.AddToRoleAsync(user, account.Role));
            await transaction.CommitAsync();
        }
    }
    private static void EnsureSuccess(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(
                "Không thể khởi tạo tài khoản/vai trò: " + string.Join("; ", result.Errors.Select(e => e.Code)));
    }
}
