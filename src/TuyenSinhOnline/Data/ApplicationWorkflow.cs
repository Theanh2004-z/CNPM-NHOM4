using Microsoft.EntityFrameworkCore;
using TuyenSinhOnline.Models;
using TuyenSinhOnline.Models.Management;

namespace TuyenSinhOnline.Data;

/// <summary>Đổi trạng thái hồ sơ đúng luật và ghi lịch sử trong cùng một giao dịch.</summary>
public static class ApplicationWorkflow
{
    public static async Task<(bool Ok, string? Error)> ChangeStatusAsync(
        ApplicationDbContext db, int applicationId, ApplicationStatusEnum toStatus,
        string officerUserId, string? note, CancellationToken ct = default)
    {
        var application = await db.AdmissionApplications.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == applicationId, ct);
        if (application is null) return (false, "Không tìm thấy hồ sơ.");

        var from = application.StatusId;
        var to = (byte)toStatus;

        // 1) Cặp trạng thái này có được phép không?
        var rule = await db.ApplicationStatusTransitions.AsNoTracking()
            .SingleOrDefaultAsync(t => t.FromStatusId == from && t.ToStatusId == to, ct);
        if (rule is null) return (false, "Không được chuyển hồ sơ sang trạng thái này.");

        // 2) Ghi chú: bắt buộc khi luật yêu cầu, tối đa 500 ký tự
        var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (rule.RequiresNote && cleanNote is null) return (false, "Vui lòng nhập ghi chú cho thao tác này.");
        if (cleanNote is { Length: > ApplicationWorkflowRules.MaxNoteLength })
            return (false, $"Ghi chú tối đa {ApplicationWorkflowRules.MaxNoteLength} ký tự.");

        // 3) Đổi trạng thái và ghi lịch sử cùng lúc (hoặc cùng thành công, hoặc cùng hủy)
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.AdmissionApplications
            .Where(a => a.Id == applicationId && a.StatusId == from)   // chống 2 cán bộ đổi cùng lúc
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.StatusId, to), ct);
        if (rows == 0) return (false, "Hồ sơ vừa được người khác cập nhật. Vui lòng tải lại trang.");

        db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
        {
            ApplicationId = applicationId,
            FromStatusId = from,
            ToStatusId = to,
            ChangedByUserId = officerUserId,
            ChangedAt = AdmissionClock.Now,
            Note = cleanNote
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return (true, null);
    }
}
