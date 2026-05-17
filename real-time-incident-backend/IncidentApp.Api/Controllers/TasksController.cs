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
[Route("api/rooms/{roomId:guid}/tasks")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<IncidentHub> _hub;

    public TasksController(AppDbContext db, IHubContext<IncidentHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    // GET /api/rooms/{roomId}/tasks
    [HttpGet]
    public async Task<IActionResult> GetTasks(Guid roomId)
    {
        var tasks = await _db.Tasks
            .Where(t => t.RoomId == roomId)
            .Include(t => t.Assignee)
            .Include(t => t.CreatedBy)
            .OrderBy(t => t.CreatedAt)
            .Select(t => ToDto(t))
            .ToListAsync();

        return Ok(tasks);
    }

    // POST /api/rooms/{roomId}/tasks
    [HttpPost]
    public async Task<IActionResult> CreateTask(Guid roomId, [FromBody] CreateTaskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { error = "Title is required." });

        var userId = GetUserId();
        var member = await _db.RoomMembers.FindAsync(roomId, userId);
        if (member is null) return Forbid();
        if (member.Role == "viewer") return Forbid();

        var validPriorities = new[] { "Low", "Medium", "High", "Critical" };
        var priority = validPriorities.Contains(request.Priority) ? request.Priority : "Medium";

        var task = new TaskItem
        {
            RoomId = roomId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Priority = priority,
            AssigneeId = request.AssigneeId,
            CreatedById = userId,
            DueAt = request.DueAt
        };

        _db.Tasks.Add(task);

        var displayName = User.FindFirstValue("displayName") ?? "Unknown";
        _db.ActivityLogs.Add(new ActivityLog
        {
            RoomId = roomId,
            ActorId = userId,
            Action = "task.created",
            TargetType = "task",
            TargetId = task.Id,
            Payload = $"{{\"title\":\"{task.Title}\"}}"
        });

        await _db.SaveChangesAsync();

        // Reload with nav properties for broadcast
        var created = await _db.Tasks
            .Include(t => t.Assignee)
            .Include(t => t.CreatedBy)
            .FirstAsync(t => t.Id == task.Id);

        var dto = ToDto(created);

        await _hub.Clients.Group($"room:{roomId}").SendAsync("TaskUpdated", dto);

        return CreatedAtAction(nameof(GetTasks), new { roomId }, dto);
    }

    // PUT /api/rooms/{roomId}/tasks/{taskId}
    [HttpPut("{taskId:guid}")]
    public async Task<IActionResult> UpdateTask(Guid roomId, Guid taskId, [FromBody] UpdateTaskRequest request)
    {
        var userId = GetUserId();
        var member = await _db.RoomMembers.FindAsync(roomId, userId);
        if (member is null) return Forbid();
        if (member.Role == "viewer") return Forbid();

        var task = await _db.Tasks
            .Include(t => t.Assignee)
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.RoomId == roomId);

        if (task is null) return NotFound();

        var validStatuses = new[] { "Todo", "InProgress", "Blocked", "Done" };
        var validPriorities = new[] { "Low", "Medium", "High", "Critical" };

        if (request.Title is not null) task.Title = request.Title.Trim();
        if (request.Description is not null) task.Description = request.Description.Trim();
        if (request.Status is not null && validStatuses.Contains(request.Status))
        {
            var previousStatus = task.Status;
            task.Status = request.Status;

            var displayName = User.FindFirstValue("displayName") ?? "Unknown";
            _db.ActivityLogs.Add(new ActivityLog
            {
                RoomId = roomId,
                ActorId = userId,
                Action = "task.status_changed",
                TargetType = "task",
                TargetId = task.Id,
                Payload = $"{{\"title\":\"{task.Title}\",\"from\":\"{previousStatus}\",\"to\":\"{request.Status}\"}}"
            });
        }
        if (request.Priority is not null && validPriorities.Contains(request.Priority)) task.Priority = request.Priority;
        if (request.AssigneeId.HasValue) task.AssigneeId = request.AssigneeId;
        if (request.DueAt.HasValue) task.DueAt = request.DueAt;

        task.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Reload nav properties after save
        await _db.Entry(task).Reference(t => t.Assignee).LoadAsync();

        var dto = ToDto(task);
        await _hub.Clients.Group($"room:{roomId}").SendAsync("TaskUpdated", dto);

        return Ok(dto);
    }

    // DELETE /api/rooms/{roomId}/tasks/{taskId}
    [HttpDelete("{taskId:guid}")]
    public async Task<IActionResult> DeleteTask(Guid roomId, Guid taskId)
    {
        var userId = GetUserId();
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        var member = await _db.RoomMembers.FindAsync(roomId, userId);

        if (member is null || (member.Role != "admin" && userRole != "admin"))
            return Forbid();

        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.RoomId == roomId);
        if (task is null) return NotFound();

        _db.Tasks.Remove(task);

        _db.ActivityLogs.Add(new ActivityLog
        {
            RoomId = roomId,
            ActorId = userId,
            Action = "task.deleted",
            TargetType = "task",
            TargetId = taskId,
            Payload = $"{{\"title\":\"{task.Title}\"}}"
        });

        await _db.SaveChangesAsync();

        await _hub.Clients.Group($"room:{roomId}").SendAsync("TaskDeleted", new { taskId });

        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")!);

    private static TaskDto ToDto(TaskItem t) => new(
        t.Id, t.RoomId, t.Title, t.Description, t.Status, t.Priority,
        t.AssigneeId, t.Assignee?.DisplayName,
        t.CreatedById, t.CreatedBy?.DisplayName ?? "",
        t.DueAt, t.CreatedAt, t.UpdatedAt);
}
