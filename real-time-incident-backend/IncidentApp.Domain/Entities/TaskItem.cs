namespace IncidentApp.Domain.Entities;

public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "Todo";   // Todo | InProgress | Blocked | Done
    public string Priority { get; set; } = "Medium"; // Low | Medium | High | Critical
    public Guid? AssigneeId { get; set; }
    public Guid CreatedById { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Nav properties
    public IncidentRoom Room { get; set; } = null!;
    public User? Assignee { get; set; }
    public User CreatedBy { get; set; } = null!;
}
