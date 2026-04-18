using System.Security.Claims;
using IncidentApp.Api.DTOs;
using IncidentApp.Api.Hubs;
using IncidentApp.Domain.Entities;
using IncidentApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace IncidentApp.Api.Controllers;

[ApiController]
[Route("api/rooms")]
[Authorize]
public class RoomsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<IncidentHub> _hub;

    public RoomsController(AppDbContext db, IHubContext<IncidentHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    // GET /api/rooms
    [HttpGet]
    public async Task<IActionResult> GetRooms()
    {
        var rooms = await _db.IncidentRooms
            .Where(r => r.ArchivedAt == null)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RoomDto(
                r.Id, r.Title, r.Description, r.Severity, r.Status,
                r.CreatedById, r.CreatedAt, r.UpdatedAt,
                r.Members.Count))
            .ToListAsync();

        return Ok(rooms);
    }

    // POST /api/rooms
    [HttpPost]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { error = "Title is required." });

        var userId = GetUserId();
        var userRole = User.FindFirstValue(ClaimTypes.Role);

        if (userRole != "admin")
            return Forbid();

        var room = new IncidentRoom
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Severity = request.Severity,
            CreatedById = userId
        };

        _db.IncidentRooms.Add(room);

        // Creator automatically joins as admin
        _db.RoomMembers.Add(new RoomMember
        {
            RoomId = room.Id,
            UserId = userId,
            Role = "admin"
        });

        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetRoom), new { id = room.Id }, ToDto(room, 1));
    }

    // GET /api/rooms/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRoom(Guid id)
    {
        var room = await _db.IncidentRooms
            .Include(r => r.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(r => r.Id == id && r.ArchivedAt == null);

        if (room is null) return NotFound();

        var detail = new RoomDetailDto(
            room.Id, room.Title, room.Description, room.Severity, room.Status,
            room.CreatedById, room.CreatedAt, room.UpdatedAt,
            room.Members.Select(m => new RoomMemberDto(
                m.UserId, m.User.DisplayName, m.User.Email, m.Role, m.JoinedAt)).ToList());

        return Ok(detail);
    }

    // PUT /api/rooms/{id}/status
    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateRoomStatusRequest request)
    {
        var validStatuses = new[] { "Open", "Investigating", "Mitigating", "Resolved", "Closed" };
        if (!validStatuses.Contains(request.Status))
            return BadRequest(new { error = "Invalid status value." });

        var userId = GetUserId();
        var room = await _db.IncidentRooms.FindAsync(id);
        if (room is null || room.ArchivedAt != null) return NotFound();

        var member = await _db.RoomMembers.FindAsync(id, userId);
        if (member is null || member.Role == "viewer") return Forbid();

        var previousStatus = room.Status;
        room.Status = request.Status;
        room.UpdatedAt = DateTime.UtcNow;

        var displayName = User.FindFirstValue("displayName") ?? "Unknown";
        var log = new ActivityLog
        {
            RoomId = id,
            ActorId = userId,
            Action = "room.status_changed",
            TargetType = "room",
            TargetId = id,
            Payload = $"{{\"from\":\"{previousStatus}\",\"to\":\"{request.Status}\"}}"
        };
        _db.ActivityLogs.Add(log);
        await _db.SaveChangesAsync();

        // Broadcast status change
        await _hub.Clients.Group($"room:{id}").SendAsync("RoomStatusChanged", new
        {
            roomId = id,
            newStatus = request.Status,
            changedBy = new { userId, displayName }
        });

        return Ok(new { roomId = id, status = request.Status });
    }

    // POST /api/rooms/{id}/join
    [HttpPost("{id:guid}/join")]
    public async Task<IActionResult> JoinRoom(Guid id)
    {
        var userId = GetUserId();
        var room = await _db.IncidentRooms.FindAsync(id);
        if (room is null || room.ArchivedAt != null) return NotFound();

        var existing = await _db.RoomMembers.FindAsync(id, userId);
        if (existing is not null) return Ok(new { message = "Already a member." });

        _db.RoomMembers.Add(new RoomMember
        {
            RoomId = id,
            UserId = userId,
            Role = "responder"
        });
        await _db.SaveChangesAsync();

        return Ok(new { message = "Joined room." });
    }

    // GET /api/rooms/{id}/members
    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid id)
    {
        var members = await _db.RoomMembers
            .Where(m => m.RoomId == id)
            .Include(m => m.User)
            .Select(m => new RoomMemberDto(
                m.UserId, m.User.DisplayName, m.User.Email, m.Role, m.JoinedAt))
            .ToListAsync();

        return Ok(members);
    }

    // GET /api/rooms/{id}/messages
    [HttpGet("{id:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (pageSize > 100) pageSize = 100;

        var messages = await _db.Messages
            .Where(m => m.RoomId == id)
            .Include(m => m.Sender)
            .OrderByDescending(m => m.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MessageDto(
                m.Id, m.RoomId, m.SenderId, m.Sender.DisplayName,
                m.Content, m.MessageType, m.SentAt))
            .ToListAsync();

        return Ok(messages.OrderBy(m => m.SentAt));
    }

    // GET /api/rooms/{id}/activity
    [HttpGet("{id:guid}/activity")]
    public async Task<IActionResult> GetActivity(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (pageSize > 100) pageSize = 100;

        var entries = await _db.ActivityLogs
            .Where(a => a.RoomId == id)
            .Include(a => a.Actor)
            .OrderByDescending(a => a.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.RoomId,
                actorId = a.ActorId,
                actorDisplayName = a.Actor.DisplayName,
                a.Action,
                a.TargetType,
                a.TargetId,
                a.Payload,
                a.OccurredAt
            })
            .ToListAsync();

        return Ok(entries.OrderBy(a => a.OccurredAt));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")!);

    private static RoomDto ToDto(IncidentRoom r, int memberCount) =>
        new(r.Id, r.Title, r.Description, r.Severity, r.Status,
            r.CreatedById, r.CreatedAt, r.UpdatedAt, memberCount);
}
