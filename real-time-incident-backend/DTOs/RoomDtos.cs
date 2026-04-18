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
