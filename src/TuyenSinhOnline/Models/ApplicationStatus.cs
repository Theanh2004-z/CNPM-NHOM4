<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;

namespace TuyenSinhOnline.Models;

public partial class ApplicationStatus
{
    public byte Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;
=======
namespace TuyenSinhOnline.Models;

public enum ApplicationStatusEnum : byte
{
    Pending = 1,
    InReview = 2,
    NeedsSupplement = 3,
    Approved = 4,
    Rejected = 5
}

public class ApplicationStatus
{
    public byte Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
>>>>>>> 51e7280e981fafafc42265ebe3032ba15f2dc522
}
