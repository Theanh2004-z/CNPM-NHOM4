using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TuyenSinhOnline.Models.Management;

public class MajorForm
{
    public int Id { get; set; }
    private string code = "", name = "";
    [Required(ErrorMessage = "Vui lòng nhập mã ngành."), StringLength(20, ErrorMessage = "{0} không được vượt quá {1} ký tự.")]
    [Display(Name = "Mã ngành")]
    public string Code { get => code; set => code = (value ?? "").Trim().ToUpperInvariant(); }
    [Required(ErrorMessage = "Vui lòng nhập tên ngành."), StringLength(200, ErrorMessage = "{0} không được vượt quá {1} ký tự.")]
    [Display(Name = "Tên ngành")]
    public string Name { get => name; set => name = (value ?? "").Trim(); }
    [StringLength(1000, ErrorMessage = "{0} không được vượt quá {1} ký tự."), Display(Name = "Mô tả")]
    public string? Description { get; set; }
    [Display(Name = "Đang sử dụng")]
    public bool IsActive { get; set; } = true;
}

public class MethodForm
{
    public int Id { get; set; }
    private string code = "", name = "";
    [Required(ErrorMessage = "Vui lòng nhập mã phương thức."), StringLength(30, ErrorMessage = "{0} không được vượt quá {1} ký tự.")]
    [Display(Name = "Mã phương thức")]
    public string Code { get => code; set => code = (value ?? "").Trim().ToUpperInvariant(); }
    [Required(ErrorMessage = "Vui lòng nhập tên phương thức."), StringLength(200, ErrorMessage = "{0} không được vượt quá {1} ký tự.")]
    [Display(Name = "Tên phương thức")]
    public string Name { get => name; set => name = (value ?? "").Trim(); }
    [StringLength(4000, ErrorMessage = "{0} không được vượt quá {1} ký tự."), Display(Name = "Điều kiện xét tuyển")]
    public string? RequirementsDescription { get; set; }
    [Display(Name = "Đang sử dụng")]
    public bool IsActive { get; set; } = true;
}

public class RoundForm : IValidatableObject
{
    public int Id { get; set; }
    private string code = "", name = "";
    [Required(ErrorMessage = "Vui lòng nhập mã đợt."), StringLength(30, ErrorMessage = "{0} không được vượt quá {1} ký tự."), Display(Name = "Mã đợt")]
    public string Code { get => code; set => code = (value ?? "").Trim().ToUpperInvariant(); }
    [Required(ErrorMessage = "Vui lòng nhập tên đợt."), StringLength(200, ErrorMessage = "{0} không được vượt quá {1} ký tự."), Display(Name = "Tên đợt")]
    public string Name { get => name; set => name = (value ?? "").Trim(); }
    [Range(2000, 2100, ErrorMessage = "Năm tuyển sinh phải từ 2000 đến 2100."), Display(Name = "Năm tuyển sinh")]
    public short AdmissionYear { get; set; } = (short)AdmissionClock.Now.Year;
    [Required(ErrorMessage = "Vui lòng nhập thời điểm mở."), Display(Name = "Thời điểm mở")]
    public DateTime? StartAt { get; set; } = AdmissionClock.Now.Date.AddDays(1);
    [Required(ErrorMessage = "Vui lòng nhập thời điểm đóng."), Display(Name = "Thời điểm đóng")]
    public DateTime? EndAt { get; set; } = AdmissionClock.Now.Date.AddDays(31);
    [Display(Name = "Đang sử dụng")]
    public bool IsActive { get; set; } = true;
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (StartAt.HasValue && EndAt.HasValue && StartAt >= EndAt)
            yield return new ValidationResult("Thời điểm đóng phải sau thời điểm mở.", [nameof(EndAt)]);
    }
}

public class QuotaForm
{
    public int Id { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn đợt tuyển sinh."), Display(Name = "Đợt tuyển sinh")]
    public int RoundId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn ngành học."), Display(Name = "Ngành học")]
    public int MajorId { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "Chỉ tiêu phải là số nguyên không âm."), Display(Name = "Chỉ tiêu")]
    public int Quota { get; set; }
    [Display(Name = "Cho phép sử dụng cấu hình này")]
    public bool IsEnabled { get; set; } = true;
    public List<int> SelectedMethodIds { get; set; } = [];
    [ValidateNever] public List<SelectListItem> Rounds { get; set; } = [];
    [ValidateNever] public List<SelectListItem> Majors { get; set; } = [];
    [ValidateNever] public List<MethodChoice> Methods { get; set; } = [];
}

public record MethodChoice(int Id, string Code, string Name, bool IsActive);
public class RoundDetails
{
    public AdmissionRound Round { get; set; } = null!;
    public List<RoundMajor> Configurations { get; set; } = [];
}

public static class AdmissionClock
{
    // Các cột datetime2 lưu giờ tuyển sinh Việt Nam (UTC+7), không phụ thuộc múi giờ máy chủ.
    public static DateTime Now => DateTime.SpecifyKind(DateTime.UtcNow.AddHours(7), DateTimeKind.Unspecified);
    public static string Status(AdmissionRound round, DateTime? now = null)
    {
        var current = now ?? Now;
        if (!round.IsActive) return "Ngừng sử dụng";
        if (current < round.StartAt) return "Chưa mở";
        return current >= round.EndAt ? "Đã đóng" : "Đang mở";
    }
}
