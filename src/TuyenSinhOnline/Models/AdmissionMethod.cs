using System;
using System.Collections.Generic;

namespace TuyenSinhOnline.Models;

public partial class AdmissionMethod
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? RequirementsDescription { get; set; }

    public string? RequiredFieldsJson { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<RoundMajorMethod> RoundMajorMethods { get; set; } = new List<RoundMajorMethod>();
}
