namespace TuyenSinhOnline.Models;

public class CandidateEducation
{
    public int Id { get; set; }
    public int CandidateProfileId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public int GraduationYear { get; set; }
    public string? AcademicInfoJson { get; set; }
    public string? Notes { get; set; }
}
