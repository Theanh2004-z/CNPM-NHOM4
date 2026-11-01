namespace TuyenSinhOnline.Models;

public class WorkspaceViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Eyebrow { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string RoleLabel { get; init; } = string.Empty;
    public string UserFullName { get; init; } = string.Empty;
    public IReadOnlyList<WorkspaceLink> Links { get; init; } = [];
}

public class WorkspaceLink
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Url { get; init; }
}
