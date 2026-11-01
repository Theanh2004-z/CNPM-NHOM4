namespace TuyenSinhOnline.Models;

public class AdmissionRound
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public short AdmissionYear { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<RoundMajor> RoundMajors { get; set; } = new List<RoundMajor>();
}
