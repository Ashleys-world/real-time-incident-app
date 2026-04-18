namespace IncidentApp.Domain.Entities;

public class Message
{
    // Client-generated UUID for idempotency
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string MessageType { get; set; } = "user"; // user | system
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    // Nav properties
    public IncidentRoom Room { get; set; } = null!;
    public User Sender { get; set; } = null!;
}
