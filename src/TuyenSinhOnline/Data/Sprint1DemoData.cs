using System.Data;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Models;

namespace TuyenSinhOnline.Data;

/// <summary>Dữ liệu demo Sprint 1, chỉ thêm bản ghi thiếu; không đặt lại cấu hình đã có.</summary>
public static class Sprint1DemoData
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        // Giữ toàn bộ lần seed trong một transaction, kể cả các khóa ngoại vừa tạo.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var it = await EnsureAsync(db.Majors, x => x.Code == "7480201", () => new Major
        {
            Code = "7480201", Name = "Công nghệ thông tin",
            Description = "Đào tạo kỹ sư phần mềm, hệ thống thông tin và an toàn mạng."
        });
        var business = await EnsureAsync(db.Majors, x => x.Code == "7340101", () => new Major
        {
            Code = "7340101", Name = "Quản trị kinh doanh",
            Description = "Đào tạo cử nhân quản trị doanh nghiệp, marketing."
        });
        var english = await EnsureAsync(db.Majors, x => x.Code == "7220201", () => new Major
        {
            Code = "7220201", Name = "Ngôn ngữ Anh",
            Description = "Đào tạo cử nhân tiếng Anh thương mại và biên phiên dịch."
        });
        var exam = await EnsureAsync(db.AdmissionMethods, x => x.Code == "PT100", () => new AdmissionMethod
        {
            Code = "PT100", Name = "Xét điểm thi tốt nghiệp THPT",
            RequirementsDescription = "Dựa trên tổng điểm tổ hợp 3 môn thi tốt nghiệp THPT."
        });
        var transcript = await EnsureAsync(db.AdmissionMethods, x => x.Code == "PT200", () => new AdmissionMethod
        {
            Code = "PT200", Name = "Xét học bạ THPT",
            RequirementsDescription = "Tổng điểm trung bình các môn cả năm lớp 12 đạt từ 18 điểm trở lên."
        });
        await EnsureAsync(db.AdmissionMethods, x => x.Code == "PT402", () => new AdmissionMethod
        {
            Code = "PT402", Name = "Xét điểm thi Đánh giá năng lực ĐHQG",
            RequirementsDescription = "Điểm bài thi ĐGNL đạt từ 650/1200 điểm."
        });
        var round = await EnsureAsync(db.AdmissionRounds, x => x.Code == "TS2027-D1", () => new AdmissionRound
        {
            Code = "TS2027-D1", Name = "Tuyển sinh Chính quy 2027 – Đợt 1", AdmissionYear = 2027,
            StartAt = new DateTime(2027, 4, 1, 0, 0, 0), EndAt = new DateTime(2027, 6, 30, 23, 59, 59)
        });
        await EnsureAsync(db.AdmissionRounds, x => x.Code == "TS2027-D2", () => new AdmissionRound
        {
            Code = "TS2027-D2", Name = "Tuyển sinh Bổ sung 2027 – Đợt 2", AdmissionYear = 2027,
            StartAt = new DateTime(2027, 8, 1, 0, 0, 0), EndAt = new DateTime(2027, 8, 31, 23, 59, 59)
        });
        await db.SaveChangesAsync();

        var configurations = new List<(RoundMajor Config, AdmissionMethod[] Methods)>();
        foreach (var sample in new[]
        {
            (Major: it, Quota: 200, Methods: new[] { exam, transcript }),
            (Major: business, Quota: 150, Methods: new[] { exam }),
            (Major: english, Quota: 100, Methods: new[] { exam })
        })
        {
            var config = await db.RoundMajors.SingleOrDefaultAsync(x => x.RoundId == round.Id && x.MajorId == sample.Major.Id);
            if (config is null)
            {
                // Không tự mở lại ngành hoặc đợt đã được quản trị ngừng sử dụng.
                if (!round.IsActive || !sample.Major.IsActive) continue;
                config = new RoundMajor { RoundId = round.Id, MajorId = sample.Major.Id, Quota = sample.Quota };
                db.RoundMajors.Add(config);
            }
            if (config.IsEnabled && round.IsActive && sample.Major.IsActive)
                configurations.Add((config, sample.Methods));
        }
        await db.SaveChangesAsync();

        foreach (var (config, methods) in configurations)
        {
            foreach (var method in methods.Where(x => x.IsActive))
            {
                // Liên kết đã tắt được giữ nguyên; không tự bật lại quyết định của quản trị.
                var exists = await db.RoundMajorMethods.AnyAsync(x => x.RoundMajorId == config.Id && x.MethodId == method.Id);
                if (!exists)
                    db.RoundMajorMethods.Add(new RoundMajorMethod { RoundMajorId = config.Id, MethodId = method.Id });
            }
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static async Task<T> EnsureAsync<T>(DbSet<T> set, Expression<Func<T, bool>> predicate, Func<T> create) where T : class
    {
        var existing = await set.SingleOrDefaultAsync(predicate);
        if (existing is not null) return existing;
        var entity = create();
        set.Add(entity);
        return entity;
    }
}
