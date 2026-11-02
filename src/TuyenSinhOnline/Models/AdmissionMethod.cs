namespace TuyenSinhOnline.Models;

public class AdmissionMethod
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? RequirementsDescription { get; set; }
    public string? RequiredFieldsJson { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<RoundMajorMethod> RoundMajorMethods { get; set; } = new List<RoundMajorMethod>();
}
