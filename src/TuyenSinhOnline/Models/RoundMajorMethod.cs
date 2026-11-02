<<<<<<< HEAD
﻿using System;
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
=======
namespace TuyenSinhOnline.Models;

public class RoundMajorMethod
{
    public int Id { get; set; }
    public int RoundMajorId { get; set; }
    public RoundMajor? RoundMajor { get; set; }
    public int MethodId { get; set; }
    public AdmissionMethod? Method { get; set; }
    public bool IsEnabled { get; set; } = true;
>>>>>>> 51e7280e981fafafc42265ebe3032ba15f2dc522
}
