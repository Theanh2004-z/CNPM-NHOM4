<<<<<<< HEAD
﻿using System;
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
=======
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
>>>>>>> 51e7280e981fafafc42265ebe3032ba15f2dc522
}
