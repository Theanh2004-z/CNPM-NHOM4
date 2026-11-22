namespace TuyenSinhOnline.Models;

/// <summary>Một dòng cập nhật trạng thái hồ sơ mà THÍ SINH được xem (không có tên cán bộ).</summary>
public class CandidateHistoryRow
{
    public string FromStatus { get; init; } = string.Empty;
    public string ToStatus { get; init; } = string.Empty;
    public DateTime ChangedAt { get; init; }
    public string? Note { get; init; }
}