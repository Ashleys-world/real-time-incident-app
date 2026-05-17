using System.Collections.Concurrent;
using IncidentApp.Infrastructure.Interfaces;

namespace IncidentApp.Infrastructure.Services;

/// <summary>
/// In-memory presence tracker. Tracks which users are connected to which rooms.
/// Not persistent — resets on restart. Replace with distributed cache (Redis) in Phase 3.
/// </summary>
public class PresenceTracker : IPresenceTracker
{
    // roomId → set of userId strings
    private readonly ConcurrentDictionary<string, HashSet<string>> _roomPresence = new();
    private readonly object _lock = new();

    public void UserJoined(string roomId, string userId)
    {
        lock (_lock)
        {
            if (!_roomPresence.ContainsKey(roomId))
                _roomPresence[roomId] = new HashSet<string>();
            _roomPresence[roomId].Add(userId);
        }
    }

    public void UserLeft(string roomId, string userId)
    {
        lock (_lock)
        {
            if (_roomPresence.TryGetValue(roomId, out var users))
                users.Remove(userId);
        }
    }

    public IReadOnlyList<string> GetOnlineUsers(string roomId)
    {
        lock (_lock)
        {
            if (_roomPresence.TryGetValue(roomId, out var users))
                return users.ToList().AsReadOnly();
            return Array.Empty<string>();
        }
    }

    public bool IsOnline(string roomId, string userId)
    {
        lock (_lock)
        {
            return _roomPresence.TryGetValue(roomId, out var users) && users.Contains(userId);
        }
    }
}
