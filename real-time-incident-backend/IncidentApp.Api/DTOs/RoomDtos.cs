namespace IncidentApp.Api.DTOs;

public record CreateRoomRequest(string Title, string? Description, string Severity);

public record UpdateRoomStatusRequest(string Status);

public record RoomDto(
    Guid Id,
    string Title,
    string? Description,
    string Severity,
    string Status,
    Guid CreatedById,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int MemberCount);

public record RoomDetailDto(
    Guid Id,
    string Title,
    string? Description,
    string Severity,
    string Status,
    Guid CreatedById,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<RoomMemberDto> Members);

public record RoomMemberDto(Guid UserId, string DisplayName, string Email, string Role, DateTime JoinedAt);

public record MessageDto(
    Guid Id,
    Guid RoomId,
    Guid SenderId,
    string SenderDisplayName,
    string Content,
    string MessageType,
    DateTime SentAt);

public record SendMessageRequest(Guid ClientMessageId, string Content);

// ── Task DTOs ──────────────────────────────────────────────────────────────

public record CreateTaskRequest(
    string Title,
    string? Description,
    string Priority,
    Guid? AssigneeId,
    DateTime? DueAt);

public record UpdateTaskRequest(
    string? Title,
    string? Description,
    string? Status,
    string? Priority,
    Guid? AssigneeId,
    DateTime? DueAt);

public record TaskDto(
    Guid Id,
    Guid RoomId,
    string Title,
    string? Description,
    string Status,
    string Priority,
    Guid? AssigneeId,
    string? AssigneeDisplayName,
    Guid CreatedById,
    string CreatedByDisplayName,
    DateTime? DueAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
