using System;
using System.Collections.Generic;

namespace TuyenSinhOnline.Models;

public partial class ApplicationStatus
{
    public byte Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;
}
