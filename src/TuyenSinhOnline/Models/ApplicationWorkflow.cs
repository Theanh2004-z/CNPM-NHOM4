namespace TuyenSinhOnline.Models;

// ===== S4-01: Chuyển trạng thái hồ sơ và lịch sử xử lý =====

/// <summary>
/// Một chuyển trạng thái được phép (ví dụ: Đang xét duyệt -> Cần bổ sung).
/// Bảng này là "luật chơi": chỉ những cặp (Từ, Đến) có trong bảng mới được phép chuyển.
/// </summary>
public class ApplicationStatusTransition
{
    public byte FromStatusId { get; set; }
    public ApplicationStatus? FromStatus { get; set; }

    public byte ToStatusId { get; set; }
    public ApplicationStatus? ToStatus { get; set; }

    /// <summary>true = cán bộ bắt buộc nhập ghi chú khi thực hiện chuyển này.</summary>
    public bool RequiresNote { get; set; }
}

/// <summary>Một dòng lịch sử: ai đổi trạng thái của hồ sơ nào, từ gì sang gì, lúc nào, ghi chú gì.</summary>
public class ApplicationStatusHistory
{
    public int Id { get; set; }

    public int ApplicationId { get; set; }
    public AdmissionApplication? Application { get; set; }

    /// <summary>Trạng thái trước khi đổi.</summary>
    public byte FromStatusId { get; set; }
    public ApplicationStatus? FromStatus { get; set; }

    /// <summary>Trạng thái sau khi đổi.</summary>
    public byte ToStatusId { get; set; }
    public ApplicationStatus? ToStatus { get; set; }

    /// <summary>Tài khoản cán bộ xử lý.</summary>
    public string ChangedByUserId { get; set; } = string.Empty;

    /// <summary>Thời điểm đổi (giờ Việt Nam, dùng AdmissionClock.Now).</summary>
    public DateTime ChangedAt { get; set; }

    /// <summary>Ghi chú của cán bộ (tối đa 500 ký tự).</summary>
    public string? Note { get; set; }
}

/// <summary>Danh sách chuyển trạng thái hợp lệ đã chốt. Được nạp vào bảng ApplicationStatusTransitions khi website khởi động.</summary>
public static class ApplicationWorkflowRules
{
    public const int MaxNoteLength = 500;

    public static readonly (ApplicationStatusEnum From, ApplicationStatusEnum To, bool RequiresNote)[] Transitions =
    [
        // Cán bộ bắt đầu xử lý hồ sơ mới nộp
        (ApplicationStatusEnum.Pending, ApplicationStatusEnum.InReview, false),
        // Xét duyệt xong: chọn một trong ba kết quả
        (ApplicationStatusEnum.InReview, ApplicationStatusEnum.Approved, false),
        (ApplicationStatusEnum.InReview, ApplicationStatusEnum.NeedsSupplement, true),  // phải ghi cần bổ sung gì
        (ApplicationStatusEnum.InReview, ApplicationStatusEnum.Rejected, true),         // phải ghi lý do
        // Hồ sơ cần bổ sung: cán bộ XÁC NHẬN đã nhận đủ (bắt buộc ghi chú) rồi mới quay lại xét duyệt
        (ApplicationStatusEnum.NeedsSupplement, ApplicationStatusEnum.InReview, true)
    ];
    // Approved và Rejected là trạng thái cuối: không có chuyển nào đi ra.
    // "Đã duyệt" chỉ có nghĩa hồ sơ hợp lệ, KHÔNG đồng nghĩa trúng tuyển.
}
