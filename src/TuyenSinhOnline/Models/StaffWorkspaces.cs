namespace TuyenSinhOnline.Models;

public class StaffIdentity
{
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

public record CatalogCount(int Total, int Enabled);

public class AdminHome : StaffIdentity
{
    public CatalogCount Majors { get; init; } = new(0, 0);
    public CatalogCount Methods { get; init; } = new(0, 0);
    public CatalogCount Rounds { get; init; } = new(0, 0);
    public CatalogCount Configurations { get; init; } = new(0, 0);
    public int OpenRounds { get; init; }
    public int ConfigurationsToReview { get; init; }
    public List<AdminRound> RecentRounds { get; init; } = [];
}

public class AdminRound
{
    public AdmissionRound Round { get; init; } = null!;
    public int Configurations { get; init; }
}

public class OfficerHome : StaffIdentity
{
    public List<OfficerRound> Rounds { get; init; } = [];
}

public class OfficerRound
{
    public AdmissionRound Round { get; init; } = null!;
    public List<OfficerMajor> Majors { get; init; } = [];
}

public class OfficerMajor
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Quota { get; init; }
    public List<string> Methods { get; init; } = [];
}
