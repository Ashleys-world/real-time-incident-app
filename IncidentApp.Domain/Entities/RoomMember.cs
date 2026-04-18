namespace IncidentApp.Domain.Entities;

public class RoomMember
{
    public Guid RoomId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "responder"; // per-room: admin | responder | viewer
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Nav properties
    public IncidentRoom Room { get; set; } = null!;
    public User User { get; set; } = null!;
}
