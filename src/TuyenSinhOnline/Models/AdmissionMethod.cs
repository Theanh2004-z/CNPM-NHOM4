<<<<<<< HEAD
﻿using System;
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
=======
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
>>>>>>> 51e7280e981fafafc42265ebe3032ba15f2dc522
}
