namespace TuyenSinhOnline.Models;

public class RoundMajorMethod
{
    public int Id { get; set; }
    public int RoundMajorId { get; set; }
    public RoundMajor? RoundMajor { get; set; }
    public int MethodId { get; set; }
    public AdmissionMethod? Method { get; set; }
    public bool IsEnabled { get; set; } = true;
}
