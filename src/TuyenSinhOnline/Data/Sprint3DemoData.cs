using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Models;
using TuyenSinhOnline.Models.Management;

namespace TuyenSinhOnline.Data;

/// <summary>
/// Dữ liệu thử Sprint 3:
/// - Tạo hồ sơ cá nhân & học vấn cho thisinh.demo@tuyensinh.local (để có thể nộp hoặc xem hồ sơ ngay).
/// - Giữ thisinh2.demo@tuyensinh.local chưa có hồ sơ cá nhân (để demo TC03).
/// - Tạo 1 hồ sơ đăng ký xét tuyển mẫu ở trạng thái "Chờ tiếp nhận".
/// </summary>
public static class Sprint3DemoData
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == "thisinh.demo@tuyensinh.local");
        if (user is null) return;

        // 1. Tạo hồ sơ cá nhân và học vấn cho thí sinh 1 nếu chưa có
        var profile = await db.CandidateProfiles.Include(x => x.Education).SingleOrDefaultAsync(x => x.UserId == user.Id);
        if (profile is null)
        {
            profile = new CandidateProfile
            {
                UserId = user.Id,
                FullName = "Nguyễn Văn Thí Sinh",
                DateOfBirth = new DateOnly(2008, 5, 12),
                PhoneNumber = "0901234567",
                ContactEmail = user.Email!,
                UpdatedAt = DateTime.UtcNow,
                Education = new CandidateEducation
                {
                    SchoolName = "THPT Chuyên Đại học Vinh",
                    GraduationYear = 2026
                }
            };
            db.CandidateProfiles.Add(profile);
            await db.SaveChangesAsync();
        }

        if (await db.AdmissionApplications.AnyAsync(a => a.UserId == user.Id)) return;

        // 2. Nguyện vọng mẫu: ngành CNTT (7480201), xét điểm thi THPT (PT100), trong đợt TS2027-D1
        var link = await db.RoundMajorMethods
            .Include(x => x.RoundMajor).ThenInclude(x => x!.Round)
            .Where(x => x.RoundMajor!.Round!.Code == "TS2027-D1"
                        && x.RoundMajor.Major!.Code == "7480201"
                        && x.Method!.Code == "PT100")
            .FirstOrDefaultAsync();
        if (link is null) return;

        await using var transaction = await db.Database.BeginTransactionAsync();
        var application = new AdmissionApplication
        {
            ApplicationCode = Guid.NewGuid().ToString("N")[..20], // mã tạm, đổi ngay bên dưới
            UserId = user.Id,
            RoundMajorMethodId = link.Id,
            StatusId = (byte)ApplicationStatusEnum.Pending,
            SubmittedAt = AdmissionClock.Now,
            FullName = profile.FullName,
            DateOfBirth = profile.DateOfBirth,
            PhoneNumber = profile.PhoneNumber,
            ContactEmail = profile.ContactEmail,
            SchoolName = profile.Education?.SchoolName ?? "THPT Chuyên Đại học Vinh",
            GraduationYear = profile.Education?.GraduationYear ?? 2026,
            Scores =
            {
                new ApplicationScore { FieldKey = "Mon1", FieldLabel = "Điểm môn 1 trong tổ hợp", Value = 8.5m },
                new ApplicationScore { FieldKey = "Mon2", FieldLabel = "Điểm môn 2 trong tổ hợp", Value = 9.0m },
                new ApplicationScore { FieldKey = "Mon3", FieldLabel = "Điểm môn 3 trong tổ hợp", Value = 8.0m }
            }
        };
        db.AdmissionApplications.Add(application);
        await db.SaveChangesAsync();

        application.ApplicationCode = AdmissionApplication.BuildCode(link.RoundMajor!.Round!.AdmissionYear, application.Id);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
