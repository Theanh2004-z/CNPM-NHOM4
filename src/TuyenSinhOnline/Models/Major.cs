using System;
using System.Collections.Generic;

namespace TuyenSinhOnline.Models;

public partial class Major
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<RoundMajor> RoundMajors { get; set; } = new List<RoundMajor>();
}
