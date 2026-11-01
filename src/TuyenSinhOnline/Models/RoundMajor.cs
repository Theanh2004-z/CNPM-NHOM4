namespace TuyenSinhOnline.Models;

public class RoundMajor
{
    public int Id { get; set; }
    public int RoundId { get; set; }
    public AdmissionRound? Round { get; set; }
    public int MajorId { get; set; }
    public Major? Major { get; set; }
    public int Quota { get; set; }
    public bool IsEnabled { get; set; } = true;

    public ICollection<RoundMajorMethod> RoundMajorMethods { get; set; } = new List<RoundMajorMethod>();
}
