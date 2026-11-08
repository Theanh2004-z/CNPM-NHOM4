using System.ComponentModel.DataAnnotations;
using System.Globalization;
using TuyenSinhOnline.Models.Management;

namespace TuyenSinhOnline.Models;

public sealed class CandidateProfileForm : IValidatableObject
{
    private static readonly EmailAddressAttribute EmailAddress = new();

    [Display(Name = "Họ và tên")]
    public string? FullName { get; set; }

    [Display(Name = "Ngày sinh")]
    public string? DateOfBirth { get; set; }

    [Display(Name = "Số điện thoại")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Email liên hệ")]
    public string? ContactEmail { get; set; }

    [Display(Name = "Trường THPT")]
    public string? SchoolName { get; set; }

    [Display(Name = "Năm tốt nghiệp hoặc dự kiến")]
    public string? GraduationYear { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var today = DateOnly.FromDateTime(AdmissionClock.Now);
        var name = FullName?.Trim() ?? string.Empty;
        if (name.Length == 0)
            yield return new("Vui lòng nhập họ và tên.", [nameof(FullName)]);
        else if (name.Length is < 2 or > 100)
            yield return new("Họ và tên phải từ 2 đến 100 ký tự.", [nameof(FullName)]);

        var birthText = DateOfBirth?.Trim();
        var validBirth = DateOnly.TryParseExact(birthText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var birth);
        if (string.IsNullOrEmpty(birthText))
            yield return new("Vui lòng nhập ngày sinh.", [nameof(DateOfBirth)]);
        else if (!validBirth)
            yield return new("Ngày sinh không hợp lệ.", [nameof(DateOfBirth)]);
        else if (birth > today)
            yield return new("Ngày sinh không được sau ngày hiện tại.", [nameof(DateOfBirth)]);

        var phone = PhoneNumber?.Trim() ?? string.Empty;
        if (phone.Length == 0)
            yield return new("Vui lòng nhập số điện thoại.", [nameof(PhoneNumber)]);
        else if (phone.Length > 32)
            yield return new("Số điện thoại không được quá 32 ký tự trước khi chuẩn hóa.", [nameof(PhoneNumber)]);
        else
        {
            var normalized = NormalizePhoneNumber(phone);
            if (normalized.Length != 10 || normalized[0] != '0' || !normalized.All(char.IsAsciiDigit))
                yield return new("Nhập số điện thoại gồm 10 chữ số bắt đầu bằng 0 hoặc dạng +84.", [nameof(PhoneNumber)]);
        }

        var email = ContactEmail?.Trim() ?? string.Empty;
        if (email.Length == 0)
            yield return new("Vui lòng nhập email liên hệ.", [nameof(ContactEmail)]);
        else if (email.Length > 256)
            yield return new("Email liên hệ không được quá 256 ký tự.", [nameof(ContactEmail)]);
        else if (!EmailAddress.IsValid(email) || email.Any(char.IsWhiteSpace) || email.Contains(',') || email.Contains(';')
            || !HasDomainLabels(email))
            yield return new("Email liên hệ không đúng định dạng.", [nameof(ContactEmail)]);

        var school = SchoolName?.Trim() ?? string.Empty;
        if (school.Length == 0)
            yield return new("Vui lòng nhập tên trường THPT.", [nameof(SchoolName)]);
        else if (school.Length > 200)
            yield return new("Tên trường THPT không được quá 200 ký tự.", [nameof(SchoolName)]);

        var yearText = GraduationYear?.Trim() ?? string.Empty;
        if (yearText.Length == 0)
            yield return new("Vui lòng nhập năm tốt nghiệp hoặc dự kiến tốt nghiệp.", [nameof(GraduationYear)]);
        else if (yearText.Length != 4 || !yearText.All(char.IsAsciiDigit))
            yield return new("Năm tốt nghiệp phải gồm 4 chữ số.", [nameof(GraduationYear)]);
        else
        {
            var year = int.Parse(yearText, CultureInfo.InvariantCulture);
            if (year < 1 || year > today.Year + 1 || (validBirth && birth <= today && year < birth.Year))
                yield return new("Năm tốt nghiệp phải từ năm sinh đến hết năm sau.", [nameof(GraduationYear)]);
        }
    }

    internal static string NormalizePhoneNumber(string value)
    {
        var compact = value.Replace(" ", string.Empty).Replace("-", string.Empty);
        return compact.StartsWith("+84", StringComparison.Ordinal) ? "0" + compact[3..] : compact;
    }

    private static bool HasDomainLabels(string email)
    {
        var labels = email[(email.IndexOf('@') + 1)..].Split('.');
        return labels.Length >= 2 && labels.All(label => label.Length > 0);
    }
}
