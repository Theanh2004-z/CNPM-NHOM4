namespace TuyenSinhOnline.Models;

public class Major
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RoundMajor> RoundMajors { get; set; } = new List<RoundMajor>();
}
