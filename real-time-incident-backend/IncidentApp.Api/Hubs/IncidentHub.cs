using System.Security.Claims;
using IncidentApp.Domain.Entities;
using IncidentApp.Infrastructure.Data;
using IncidentApp.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace IncidentApp.Api.Hubs;

[Authorize]
public class IncidentHub : Hub
{
    private readonly AppDbContext _db;
    private readonly IPresenceTracker _presence;

    public IncidentHub(AppDbContext db, IPresenceTracker presence)
    {
        _db = db;
        _presence = presence;
    }

    // ── Connection lifecycle ──────────────────────────────────────────────

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Remove user from all rooms they were tracking
        // We store room membership in connection context via Items
        if (Context.Items.TryGetValue("rooms", out var roomsObj) &&
            roomsObj is HashSet<string> rooms)
        {
            var userId = GetUserId();
            var displayName = GetDisplayName();

            foreach (var roomId in rooms)
            {
                _presence.UserLeft(roomId, userId);

                await Clients.Group(roomId).SendAsync("UserLeft", new
                {
                    userId,
                    displayName
                });

                await BroadcastPresence(roomId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    // ── Client → Server methods ───────────────────────────────────────────

    /// <summary>Join SignalR group for an incident room.</summary>
    public async Task JoinRoom(string roomId)
    {
        var userId = GetUserId();
        var parsedRoomId = Guid.Parse(roomId);

        // Verify user is a member of this room
        var isMember = await _db.RoomMembers
            .AnyAsync(m => m.RoomId == parsedRoomId && m.UserId == Guid.Parse(userId));

        if (!isMember)
        {
            await Clients.Caller.SendAsync("Error", "You are not a member of this room.");
            return;
        }

        var groupName = $"room:{roomId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _presence.UserJoined(groupName, userId);

        // Track in connection context
        if (!Context.Items.ContainsKey("rooms"))
            Context.Items["rooms"] = new HashSet<string>();
        ((HashSet<string>)Context.Items["rooms"]!).Add(groupName);

        var displayName = GetDisplayName();

        await Clients.Group(groupName).SendAsync("UserJoined", new
        {
            userId,
            displayName
        });

        await BroadcastPresence(groupName);
    }

    /// <summary>Leave a room's SignalR group.</summary>
    public async Task LeaveRoom(string roomId)
    {
        var groupName = $"room:{roomId}";
        var userId = GetUserId();
        var displayName = GetDisplayName();

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _presence.UserLeft(groupName, userId);

        if (Context.Items.TryGetValue("rooms", out var roomsObj) && roomsObj is HashSet<string> rooms)
            rooms.Remove(groupName);

        await Clients.Group(groupName).SendAsync("UserLeft", new { userId, displayName });
        await BroadcastPresence(groupName);
    }

    /// <summary>Send a chat message to the room. Persisted and broadcast.</summary>
    public async Task SendMessage(string roomId, string content, string clientMessageId)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        var userId = GetUserId();
        var parsedRoomId = Guid.Parse(roomId);
        var parsedMessageId = Guid.Parse(clientMessageId);

        // Idempotency: if message already stored, do nothing
        if (await _db.Messages.AnyAsync(m => m.Id == parsedMessageId)) return;

        var user = await _db.Users.FindAsync(Guid.Parse(userId));
        if (user is null) return;

        var message = new Message
        {
            Id = parsedMessageId,
            RoomId = parsedRoomId,
            SenderId = Guid.Parse(userId),
            Content = content.Trim(),
            MessageType = "user"
        };

        _db.Messages.Add(message);
        await _db.SaveChangesAsync();

        var groupName = $"room:{roomId}";
        await Clients.Group(groupName).SendAsync("MessageReceived", new
        {
            id = message.Id,
            roomId = parsedRoomId,
            senderId = message.SenderId,
            displayName = user.DisplayName,
            content = message.Content,
            messageType = message.MessageType,
            sentAt = message.SentAt
        });
    }

    /// <summary>Notify room that the caller is typing.</summary>
    public async Task StartTyping(string roomId)
    {
        var userId = GetUserId();
        var displayName = GetDisplayName();

        await Clients.OthersInGroup($"room:{roomId}").SendAsync("UserTyping", new
        {
            userId,
            displayName
        });
    }

    /// <summary>Notify room that the caller stopped typing.</summary>
    public async Task StopTyping(string roomId)
    {
        var userId = GetUserId();

        await Clients.OthersInGroup($"room:{roomId}").SendAsync("UserStoppedTyping", new
        {
            userId
        });
    }

    /// <summary>Update a task's status and broadcast to the room.</summary>
    public async Task UpdateTaskStatus(string roomId, string taskId, string newStatus)
    {
        var validStatuses = new[] { "Todo", "InProgress", "Blocked", "Done" };
        if (!validStatuses.Contains(newStatus))
            throw new HubException("Invalid task status.");

        var userId = GetUserId();
        var parsedRoomId = Guid.Parse(roomId);
        var parsedTaskId = Guid.Parse(taskId);

        var member = await _db.RoomMembers.FindAsync(parsedRoomId, Guid.Parse(userId));
        if (member is null || member.Role == "viewer")
            throw new HubException("Not authorised to update tasks.");

        var task = await _db.Tasks
            .Include(t => t.Assignee)
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == parsedTaskId && t.RoomId == parsedRoomId);

        if (task is null) throw new HubException("Task not found.");

        var previousStatus = task.Status;
        task.Status = newStatus;
        task.UpdatedAt = DateTime.UtcNow;

        _db.ActivityLogs.Add(new ActivityLog
        {
            RoomId = parsedRoomId,
            ActorId = Guid.Parse(userId),
            Action = "task.status_changed",
            TargetType = "task",
            TargetId = parsedTaskId,
            Payload = $"{{\"title\":\"{task.Title}\",\"from\":\"{previousStatus}\",\"to\":\"{newStatus}\"}}"
        });

        await _db.SaveChangesAsync();

        var dto = new
        {
            task.Id, task.RoomId, task.Title, task.Description,
            task.Status, task.Priority,
            task.AssigneeId, AssigneeDisplayName = task.Assignee?.DisplayName,
            task.CreatedById, CreatedByDisplayName = task.CreatedBy?.DisplayName ?? "",
            task.DueAt, task.CreatedAt, task.UpdatedAt
        };

        await Clients.Group($"room:{roomId}").SendAsync("TaskUpdated", dto);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private string GetUserId() =>
        Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? Context.User?.FindFirstValue("sub")
        ?? throw new HubException("Unauthenticated");

    private string GetDisplayName() =>
        Context.User?.FindFirstValue("displayName") ?? "Unknown";

    private async Task BroadcastPresence(string groupName)
    {
        var onlineUserIds = _presence.GetOnlineUsers(groupName);

        // Fetch display names for online users
        var userGuids = onlineUserIds
            .Select(id => Guid.TryParse(id, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToList();

        var users = await _db.Users
            .Where(u => userGuids.Contains(u.Id))
            .Select(u => new { userId = u.Id.ToString(), u.DisplayName })
            .ToListAsync();

        await Clients.Group(groupName).SendAsync("PresenceUpdated", new { onlineUsers = users });
    }
}
