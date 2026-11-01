namespace TuyenSinhOnline.Security;

public static class RoleNames
{
    public const string Candidate = "ThiSinh";
    public const string AdmissionsOfficer = "CanBoTuyenSinh";
    public const string Administrator = "QuanTriVien";

    public static readonly string[] All = [Candidate, AdmissionsOfficer, Administrator];
}
