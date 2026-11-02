namespace TuyenSinhOnline.Models;

public class CandidateHome
{
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public List<AdmissionRound> Rounds { get; init; } = [];
    public AdmissionRound? SelectedRound { get; init; }
    public string RoundStatus { get; init; } = string.Empty;
    public string Search { get; init; } = string.Empty;
    public List<CandidateMajor> Majors { get; init; } = [];
}

public class CandidateMajor
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int Quota { get; init; }
    public List<CandidateMethod> Methods { get; init; } = [];
}

public class CandidateMethod
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Requirements { get; init; }
}
