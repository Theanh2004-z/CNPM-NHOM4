using System;
using System.Collections.Generic;

namespace TuyenSinhOnline.Models;

public partial class RoundMajor
{
    public int Id { get; set; }

    public int RoundId { get; set; }

    public int MajorId { get; set; }

    public int Quota { get; set; }

    public bool IsEnabled { get; set; }

    public virtual Major Major { get; set; } = null!;

    public virtual AdmissionRound Round { get; set; } = null!;

    public virtual ICollection<RoundMajorMethod> RoundMajorMethods { get; set; } = new List<RoundMajorMethod>();
}
