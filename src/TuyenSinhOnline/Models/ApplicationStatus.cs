namespace TuyenSinhOnline.Models;

public enum ApplicationStatusEnum : byte
{
    Pending = 1,
    InReview = 2,
    NeedsSupplement = 3,
    Approved = 4,
    Rejected = 5
}

public class ApplicationStatus
{
    public byte Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
