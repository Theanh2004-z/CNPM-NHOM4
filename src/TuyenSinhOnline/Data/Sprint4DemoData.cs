using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Models;
using TuyenSinhOnline.Models.Management;

namespace TuyenSinhOnline.Data;

/// <summary>
/// Dữ liệu mẫu chuẩn hóa cho Sprint 4:
/// - Thí sinh 1 (thisinh.demo@tuyensinh.local): Đã hoàn thiện lý lịch & học bạ, có 3 hồ sơ nộp ở các trạng thái khác nhau.
///   + Hồ sơ 1 (CNTT - PT100): Trạng thái "Đã duyệt", có đủ 4 bản ghi lịch sử xử lý (để demo xem lịch sử và ẩn ghi chú nội bộ).
///   + Hồ sơ 2 (CNTT - PT200): Trạng thái "Chờ tiếp nhận" (mới nộp, chưa có lịch sử).
///   + Hồ sơ 3 (Ngôn ngữ Anh - PT100): Trạng thái "Cần bổ sung", có 2 bản ghi lịch sử kèm lý do công khai.
/// - Thí sinh 2 (thisinh2.demo@tuyensinh.local): Chưa có hồ sơ cá nhân (để demo rào chắn điều kiện và kiểm tra chống IDOR).
/// - Đợt 1 (TS2027-D1): Có 3 hồ sơ để demo danh sách cán bộ và thống kê tuyển sinh.
/// - Đợt 2 (TS2027-D2): Có 0 hồ sơ để demo trường hợp đợt rỗng an toàn trong thống kê.
/// </summary>
public static class Sprint4DemoData
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        var candidate = await db.Users.SingleOrDefaultAsync(u => u.Email == "thisinh.demo@tuyensinh.local");
        if (candidate is null) return;

        var officer = await db.Users.SingleOrDefaultAsync(u => u.Email == "canbo.demo@tuyensinh.local");
        var officerId = officer?.Id ?? candidate.Id;

        // 1. Tạo hồ sơ cá nhân và học vấn cho Thí sinh 1 nếu chưa có
        var profile = await db.CandidateProfiles.Include(x => x.Education).SingleOrDefaultAsync(x => x.UserId == candidate.Id);
        if (profile is null)
        {
            profile = new CandidateProfile
            {
                UserId = candidate.Id,
                FullName = "Nguyễn Văn Thí Sinh",
                DateOfBirth = new DateOnly(2008, 5, 12),
                PhoneNumber = "0901234567",
                ContactEmail = candidate.Email!,
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

        // Nếu đã có hồ sơ nộp rồi thì không seed lại
        if (await db.AdmissionApplications.AnyAsync(a => a.UserId == candidate.Id)) return;

        var now = AdmissionClock.Now;

        // Tìm các liên kết đợt - ngành - phương thức trong đợt TS2027-D1
        var linkItExam = await db.RoundMajorMethods
            .Include(x => x.RoundMajor).ThenInclude(x => x!.Round)
            .Where(x => x.RoundMajor!.Round!.Code == "TS2027-D1"
                        && x.RoundMajor.Major!.Code == "7480201"
                        && x.Method!.Code == "PT100")
            .FirstOrDefaultAsync();

        var linkItTranscript = await db.RoundMajorMethods
            .Include(x => x.RoundMajor).ThenInclude(x => x!.Round)
            .Where(x => x.RoundMajor!.Round!.Code == "TS2027-D1"
                        && x.RoundMajor.Major!.Code == "7480201"
                        && x.Method!.Code == "PT200")
            .FirstOrDefaultAsync();

        var linkEnglishExam = await db.RoundMajorMethods
            .Include(x => x.RoundMajor).ThenInclude(x => x!.Round)
            .Where(x => x.RoundMajor!.Round!.Code == "TS2027-D1"
                        && x.RoundMajor.Major!.Code == "7220201"
                        && x.Method!.Code == "PT100")
            .FirstOrDefaultAsync();

        if (linkItExam is null) return;

        await using var transaction = await db.Database.BeginTransactionAsync();

        // -------------------------------------------------------------
        // HỒ SƠ 1: Ngành CNTT, PT100 -> Trạng thái ĐÃ DUYỆT (4 lịch sử)
        // -------------------------------------------------------------
        var app1 = new AdmissionApplication
        {
            ApplicationCode = "HS2027-000001",
            UserId = candidate.Id,
            RoundMajorMethodId = linkItExam.Id,
            StatusId = (byte)ApplicationStatusEnum.Approved,
            SubmittedAt = now.AddDays(-2),
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
        db.AdmissionApplications.Add(app1);
        await db.SaveChangesAsync();

        app1.ApplicationCode = AdmissionApplication.BuildCode(linkItExam.RoundMajor!.Round!.AdmissionYear, app1.Id);
        await db.SaveChangesAsync();

        // Thêm 4 bản ghi lịch sử xử lý cho Hồ sơ 1
        db.ApplicationStatusHistories.AddRange(
            new ApplicationStatusHistory
            {
                ApplicationId = app1.Id,
                FromStatusId = (byte)ApplicationStatusEnum.Pending,
                ToStatusId = (byte)ApplicationStatusEnum.InReview,
                ChangedByUserId = officerId,
                ChangedAt = now.AddDays(-2).AddHours(2),
                Note = null
            },
            new ApplicationStatusHistory
            {
                ApplicationId = app1.Id,
                FromStatusId = (byte)ApplicationStatusEnum.InReview,
                ToStatusId = (byte)ApplicationStatusEnum.NeedsSupplement,
                ChangedByUserId = officerId,
                ChangedAt = now.AddDays(-1).AddHours(-4),
                Note = "Vui lòng bổ sung bản sao công chứng CCCD và học bạ THPT."
            },
            new ApplicationStatusHistory
            {
                ApplicationId = app1.Id,
                FromStatusId = (byte)ApplicationStatusEnum.NeedsSupplement,
                ToStatusId = (byte)ApplicationStatusEnum.InReview,
                ChangedByUserId = officerId,
                ChangedAt = now.AddDays(-1).AddHours(1),
                Note = "Ghi chú nội bộ: Đã nhận đủ hồ sơ bổ sung qua đường bưu điện."
            },
            new ApplicationStatusHistory
            {
                ApplicationId = app1.Id,
                FromStatusId = (byte)ApplicationStatusEnum.InReview,
                ToStatusId = (byte)ApplicationStatusEnum.Approved,
                ChangedByUserId = officerId,
                ChangedAt = now.AddHours(-3),
                Note = "Ghi chú nội bộ: Hồ sơ hợp lệ, đủ điều kiện xét tuyển đợt 1."
            }
        );

        // -------------------------------------------------------------
        // HỒ SƠ 2: Ngành CNTT, PT200 -> Trạng thái CHỜ TIẾP NHẬN
        // -------------------------------------------------------------
        if (linkItTranscript is not null)
        {
            var app2 = new AdmissionApplication
            {
                ApplicationCode = "HS2027-000002",
                UserId = candidate.Id,
                RoundMajorMethodId = linkItTranscript.Id,
                StatusId = (byte)ApplicationStatusEnum.Pending,
                SubmittedAt = now.AddHours(-5),
                FullName = profile.FullName,
                DateOfBirth = profile.DateOfBirth,
                PhoneNumber = profile.PhoneNumber,
                ContactEmail = profile.ContactEmail,
                SchoolName = profile.Education?.SchoolName ?? "THPT Chuyên Đại học Vinh",
                GraduationYear = profile.Education?.GraduationYear ?? 2026,
                Scores =
                {
                    new ApplicationScore { FieldKey = "TongHocBa", FieldLabel = "Tổng điểm trung bình học bạ 3 năm", Value = 26.5m }
                }
            };
            db.AdmissionApplications.Add(app2);
            await db.SaveChangesAsync();

            app2.ApplicationCode = AdmissionApplication.BuildCode(linkItTranscript.RoundMajor!.Round!.AdmissionYear, app2.Id);
            await db.SaveChangesAsync();
        }

        // -------------------------------------------------------------
        // HỒ SƠ 3: Ngành Ngôn ngữ Anh, PT100 -> Trạng thái CẦN BỔ SUNG
        // -------------------------------------------------------------
        if (linkEnglishExam is not null)
        {
            var app3 = new AdmissionApplication
            {
                ApplicationCode = "HS2027-000003",
                UserId = candidate.Id,
                RoundMajorMethodId = linkEnglishExam.Id,
                StatusId = (byte)ApplicationStatusEnum.NeedsSupplement,
                SubmittedAt = now.AddDays(-1),
                FullName = profile.FullName,
                DateOfBirth = profile.DateOfBirth,
                PhoneNumber = profile.PhoneNumber,
                ContactEmail = profile.ContactEmail,
                SchoolName = profile.Education?.SchoolName ?? "THPT Chuyên Đại học Vinh",
                GraduationYear = profile.Education?.GraduationYear ?? 2026,
                Scores =
                {
                    new ApplicationScore { FieldKey = "Mon1", FieldLabel = "Điểm môn 1 trong tổ hợp", Value = 7.0m },
                    new ApplicationScore { FieldKey = "Mon2", FieldLabel = "Điểm môn 2 trong tổ hợp", Value = 8.0m },
                    new ApplicationScore { FieldKey = "Mon3", FieldLabel = "Điểm môn 3 trong tổ hợp", Value = 9.0m }
                }
            };
            db.AdmissionApplications.Add(app3);
            await db.SaveChangesAsync();

            app3.ApplicationCode = AdmissionApplication.BuildCode(linkEnglishExam.RoundMajor!.Round!.AdmissionYear, app3.Id);
            await db.SaveChangesAsync();

            db.ApplicationStatusHistories.AddRange(
                new ApplicationStatusHistory
                {
                    ApplicationId = app3.Id,
                    FromStatusId = (byte)ApplicationStatusEnum.Pending,
                    ToStatusId = (byte)ApplicationStatusEnum.InReview,
                    ChangedByUserId = officerId,
                    ChangedAt = now.AddHours(-18),
                    Note = null
                },
                new ApplicationStatusHistory
                {
                    ApplicationId = app3.Id,
                    FromStatusId = (byte)ApplicationStatusEnum.InReview,
                    ToStatusId = (byte)ApplicationStatusEnum.NeedsSupplement,
                    ChangedByUserId = officerId,
                    ChangedAt = now.AddHours(-10),
                    Note = "Vui lòng bổ sung chứng chỉ ngoại ngữ IELTS hoặc tương đương theo quy định."
                }
            );
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
