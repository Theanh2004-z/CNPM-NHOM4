namespace TuyenSinhOnline.Models;

// ===== S3-02: Biểu mẫu nộp hồ sơ xét tuyển =====

/// <summary>Một trường dữ liệu mà thí sinh phải nhập theo phương thức (ví dụ: điểm Toán, 0–10).</summary>
public record MethodField(string Key, string Label, decimal Min, decimal Max);

/// <summary>
/// Danh sách trường dữ liệu theo từng phương thức.
/// Tạm đặt cứng theo MÃ phương thức trong dữ liệu mẫu (PT100, PT200, PT402).
/// Muốn thêm phương thức mới: thêm một dòng vào đây.
/// </summary>
public static class MethodFieldCatalog
{
    public static MethodField[] For(string methodCode) => methodCode switch
    {
        "PT100" =>
        [
            new("Mon1", "Điểm môn 1 trong tổ hợp", 0, 10),
            new("Mon2", "Điểm môn 2 trong tổ hợp", 0, 10),
            new("Mon3", "Điểm môn 3 trong tổ hợp", 0, 10)
        ],
        "PT200" => [new("TongHocBa", "Tổng điểm TB 3 môn cả năm lớp 12", 0, 30)],
        "PT402" => [new("DiemDGNL", "Điểm bài thi ĐGNL", 0, 1200)],
        _ => []
    };
}

/// <summary>Phương thức áp dụng cho một ngành trong đợt (1 dòng của bảng RoundMajorMethods).</summary>
public class ApplyMethod
{
    public int RoundMajorMethodId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Requirements { get; init; }
    public MethodField[] Fields => MethodFieldCatalog.For(Code);
}

/// <summary>Ngành đang mở trong đợt (1 dòng của bảng RoundMajors) kèm các phương thức hợp lệ.</summary>
public class FormMajor
{
    public int RoundMajorId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public List<ApplyMethod> Methods { get; init; } = [];
}

/// <summary>Dữ liệu thí sinh gửi lên từ form.</summary>
public class ApplicationFormInput
{
    public int? RoundId { get; set; }
    public int? RoundMajorId { get; set; }
    public int? RoundMajorMethodId { get; set; }
    /// <summary>Điểm theo từng trường: Scores[Mon1], Scores[Mon2]...</summary>
    public Dictionary<string, string?> Scores { get; set; } = [];
}

/// <summary>Dữ liệu đưa sang View để vẽ form.</summary>
public class ApplicationFormPage
{
    public CandidateProfile? Profile { get; init; }          // null = chưa tạo hồ sơ cá nhân
    public AdmissionRound? Round { get; init; }              // null = chưa có đợt đang mở
    public List<FormMajor> Majors { get; init; } = [];
    public ApplicationFormInput Input { get; init; } = new();
    public bool IncompleteProfile => Profile is null 
        || string.IsNullOrWhiteSpace(Profile.FullName) 
        || string.IsNullOrWhiteSpace(Profile.PhoneNumber) 
        || string.IsNullOrWhiteSpace(Profile.ContactEmail) 
        || Profile.DateOfBirth == default;
    public bool MissingEducation => !IncompleteProfile && (Profile?.Education is null || string.IsNullOrWhiteSpace(Profile.Education.SchoolName) || Profile.Education.GraduationYear <= 0);
    public bool CanApply => !IncompleteProfile && !MissingEducation && Round is not null && Majors.Count > 0;
}

/// <summary>Dữ liệu danh sách hồ sơ đã nộp của thí sinh (S3-05).</summary>
public class ApplicationListViewModel
{
    public List<AdmissionApplication> Applications { get; init; } = [];
    public List<AdmissionRound> Rounds { get; init; } = [];
    public int? SelectedRoundId { get; init; }
    public string Search { get; init; } = string.Empty;
    public int TotalCount { get; init; }
}
