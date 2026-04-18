namespace IncidentApp.Infrastructure.Interfaces;

public interface IPresenceTracker
{
    void UserJoined(string roomId, string userId);
    void UserLeft(string roomId, string userId);
    IReadOnlyList<string> GetOnlineUsers(string roomId);
    bool IsOnline(string roomId, string userId);
}
