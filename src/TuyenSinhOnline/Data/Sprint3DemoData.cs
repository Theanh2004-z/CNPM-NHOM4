using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Models;
using TuyenSinhOnline.Models.Management;

namespace TuyenSinhOnline.Data;

/// <summary>Dữ liệu thử Sprint 3: một hồ sơ đăng ký mẫu cho thí sinh demo. Chỉ thêm khi chưa có, không đụng dữ liệu cũ.</summary>
public static class Sprint3DemoData
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == "thisinh.demo@tuyensinh.local");
        if (user is null) return;
        if (await db.AdmissionApplications.AnyAsync(a => a.UserId == user.Id)) return;

        // Nguyện vọng mẫu: ngành CNTT, xét điểm thi THPT, trong đợt TS2027-D1
        var link = await db.RoundMajorMethods
            .Include(x => x.RoundMajor).ThenInclude(x => x!.Round)
            .Where(x => x.RoundMajor!.Round!.Code == "TS2027-D1"
                        && x.RoundMajor.Major!.Code == "7480201"
                        && x.Method!.Code == "PT100")
            .FirstOrDefaultAsync();
        if (link is null) return;

        // Lấy thông tin cá nhân nếu thí sinh đã tạo hồ sơ, không thì dùng giá trị mẫu
        var profile = await db.CandidateProfiles.Include(x => x.Education).SingleOrDefaultAsync(x => x.UserId == user.Id);

        await using var transaction = await db.Database.BeginTransactionAsync();
        var application = new AdmissionApplication
        {
            ApplicationCode = Guid.NewGuid().ToString("N")[..20],   // mã tạm, đổi ngay bên dưới
            UserId = user.Id,
            RoundMajorMethodId = link.Id,
            StatusId = (byte)ApplicationStatusEnum.Pending,
            SubmittedAt = AdmissionClock.Now,
            FullName = profile?.FullName ?? user.FullName,
            DateOfBirth = profile?.DateOfBirth ?? new DateOnly(2008, 5, 12),
            PhoneNumber = profile?.PhoneNumber ?? "0900000001",
            ContactEmail = profile?.ContactEmail ?? user.Email!,
            SchoolName = profile?.Education?.SchoolName ?? "THPT Demo",
            GraduationYear = profile?.Education?.GraduationYear ?? 2026,
            Scores =
            {
                new ApplicationScore { FieldKey = "Mon1", FieldLabel = "Điểm môn 1 trong tổ hợp", Value = 8.5m },
                new ApplicationScore { FieldKey = "Mon2", FieldLabel = "Điểm môn 2 trong tổ hợp", Value = 9m },
                new ApplicationScore { FieldKey = "Mon3", FieldLabel = "Điểm môn 3 trong tổ hợp", Value = 8m }
            }
        };
        db.AdmissionApplications.Add(application);
        await db.SaveChangesAsync();   // có Id rồi mới sinh được mã thật

        application.ApplicationCode = AdmissionApplication.BuildCode(link.RoundMajor!.Round!.AdmissionYear, application.Id);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
