namespace IncidentApp.Domain.Entities;

public class ActivityLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = string.Empty;   // e.g. room.status_changed, task.created
    public string? TargetType { get; set; }               // task | room | message
    public Guid? TargetId { get; set; }
    public string? Payload { get; set; }                  // JSON string (diff / metadata)
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    // Nav properties
    public IncidentRoom Room { get; set; } = null!;
    public User Actor { get; set; } = null!;
}
