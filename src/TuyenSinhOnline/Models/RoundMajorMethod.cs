using System;
using System.Collections.Generic;

namespace TuyenSinhOnline.Models;

public partial class RoundMajorMethod
{
    public int Id { get; set; }

    public int RoundMajorId { get; set; }

    public int MethodId { get; set; }

    public bool IsEnabled { get; set; }

    public virtual AdmissionMethod Method { get; set; } = null!;

    public virtual RoundMajor RoundMajor { get; set; } = null!;
}
