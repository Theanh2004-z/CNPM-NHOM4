namespace TuyenSinhOnline.Models;

// ===== S3-01: Hồ sơ đăng ký xét tuyển =====

/// <summary>
/// Một hồ sơ đăng ký xét tuyển của thí sinh (một nguyện vọng = một ngành + một phương thức trong một đợt).
/// Thông tin cá nhân được CHÉP lại tại thời điểm gửi, nên sau này thí sinh sửa hồ sơ cá nhân thì hồ sơ đã nộp không đổi.
/// </summary>
public class AdmissionApplication
{
    public int Id { get; set; }

    /// <summary>Mã hồ sơ duy nhất, ví dụ HS2027-000001 (có chỉ mục UNIQUE trong database).</summary>
    public string ApplicationCode { get; set; } = string.Empty;

    /// <summary>Tài khoản thí sinh đã nộp.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Dòng "ngành + phương thức trong đợt" mà thí sinh chọn. Từ đây suy ra được đợt, ngành, phương thức.</summary>
    public int RoundMajorMethodId { get; set; }
    public RoundMajorMethod? RoundMajorMethod { get; set; }

    /// <summary>Trạng thái hồ sơ; mặc định là Chờ tiếp nhận.</summary>
    public byte StatusId { get; set; } = (byte)ApplicationStatusEnum.Pending;
    public ApplicationStatus? Status { get; set; }

    public DateTime SubmittedAt { get; set; }   // giờ Việt Nam, dùng AdmissionClock.Now

    // ---- Bản chụp thông tin cá nhân tại thời điểm gửi ----
    public string FullName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string SchoolName { get; set; } = string.Empty;
    public int GraduationYear { get; set; }

    /// <summary>Các điểm đã nhập theo phương thức (Mon1, Mon2, TongHocBa, DiemDGNL...).</summary>
    public ICollection<ApplicationScore> Scores { get; set; } = new List<ApplicationScore>();

    /// <summary>Quy tắc đặt mã: HS + năm tuyển sinh + số thứ tự (Id) 6 chữ số.</summary>
    public static string BuildCode(int admissionYear, int id) => $"HS{admissionYear}-{id:D6}";
}

/// <summary>Một giá trị điểm của hồ sơ (mỗi trường của phương thức là một dòng).</summary>
public class ApplicationScore
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public AdmissionApplication? Application { get; set; }
    public string FieldKey { get; set; } = string.Empty;    // trùng với Key trong MethodFieldCatalog (S3-02)
    public string FieldLabel { get; set; } = string.Empty;  // nhãn hiển thị tại thời điểm gửi
    public decimal Value { get; set; }
}
