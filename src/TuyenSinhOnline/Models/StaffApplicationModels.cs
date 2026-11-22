namespace TuyenSinhOnline.Models;

// ===== S4-02, S4-03: màn hình cán bộ xử lý hồ sơ =====

/// <summary>Một dòng trong danh sách hồ sơ của cán bộ.</summary>
public class StaffApplicationRow
{
    public int Id { get; init; }
    public string ApplicationCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string MajorCode { get; init; } = string.Empty;
    public string MajorName { get; init; } = string.Empty;
    public string MethodName { get; init; } = string.Empty;
    public string RoundName { get; init; } = string.Empty;
    public byte StatusId { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public DateTime SubmittedAt { get; init; }
}

/// <summary>Dữ liệu trang danh sách: kết quả + các ô tìm kiếm/lọc.</summary>
public class StaffApplicationListViewModel
{
    public List<StaffApplicationRow> Items { get; init; } = [];
    public List<Major> Majors { get; init; } = [];
    public List<ApplicationStatus> Statuses { get; init; } = [];
    public string Search { get; init; } = string.Empty;
    public int? MajorId { get; init; }
    public byte? StatusId { get; init; }
    public int Page { get; init; } = 1;
    public int TotalPages { get; init; } = 1;
    public int TotalCount { get; init; }
}

/// <summary>Một dòng lịch sử xử lý để hiển thị.</summary>
public class StaffHistoryRow
{
    public string FromStatus { get; init; } = string.Empty;
    public string ToStatus { get; init; } = string.Empty;
    public string ChangedBy { get; init; } = string.Empty;
    public DateTime ChangedAt { get; init; }
    public string? Note { get; init; }
}

/// <summary>Một trạng thái mà hồ sơ được phép chuyển tới (lấy từ bảng chuyển trạng thái của S4-01).</summary>
public class StaffTransitionOption
{
    public byte ToStatusId { get; init; }
    public string ToStatusName { get; init; } = string.Empty;
    public bool RequiresNote { get; init; }
}

/// <summary>Dữ liệu trang chi tiết hồ sơ.</summary>
public class StaffApplicationDetailViewModel
{
    public AdmissionApplication Application { get; init; } = null!;
    public List<StaffHistoryRow> History { get; init; } = [];
    public List<StaffTransitionOption> Transitions { get; init; } = [];
    /// <summary>true = người xem là cán bộ tuyển sinh (được xử lý). Quản trị viên chỉ xem.</summary>
    public bool CanProcess { get; init; }
}

// ===== S4-05, S4-06: Thống kê tuyển sinh =====

/// <summary>Một dòng thống kê (theo ngành hoặc theo phương thức).</summary>
public class StatisticRow
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Count { get; init; }
    public double Percentage { get; init; }
}

/// <summary>Một dòng thống kê theo trạng thái hồ sơ.</summary>
public class StatusStatisticRow
{
    public byte StatusId { get; init; }
    public string StatusCode { get; init; } = string.Empty;
    public string StatusName { get; init; } = string.Empty;
    public int Count { get; init; }
    public double Percentage { get; init; }
    public string BadgeClass { get; init; } = string.Empty;
}

/// <summary>Dữ liệu màn hình Thống kê tuyển sinh cho Cán bộ và Quản trị viên.</summary>
public class StaffStatisticsViewModel
{
    public int? RoundId { get; init; }
    public List<AdmissionRound> Rounds { get; init; } = [];
    public int TotalApplications { get; init; }
    public List<StatisticRow> MajorRows { get; init; } = [];
    public List<StatisticRow> MethodRows { get; init; } = [];
    public List<StatusStatisticRow> StatusRows { get; init; } = [];
}