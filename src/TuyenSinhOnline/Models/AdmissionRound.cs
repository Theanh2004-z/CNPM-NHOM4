using System;
using System.Collections.Generic;

namespace TuyenSinhOnline.Models;

public partial class AdmissionRound
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public short AdmissionYear { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<RoundMajor> RoundMajors { get; set; } = new List<RoundMajor>();
}
