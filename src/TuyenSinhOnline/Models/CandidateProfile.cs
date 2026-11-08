namespace TuyenSinhOnline.Models;

public class CandidateProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public CandidateEducation? Education { get; set; }
}
