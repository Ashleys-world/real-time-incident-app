# Real-Time Incident Coordination App — Phase 1

## Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | 9.0+ |
| Node.js | 18+ |
| Angular CLI | 17+ (`npm i -g @angular/cli`) |
| PostgreSQL | 14+ |
| dotnet-ef | `dotnet tool install -g dotnet-ef` |

---

## Project Structure

```
real-time-incident-app/
├── real-time-incident-backend/   # ASP.NET Core 9 API (IncidentApp.Api)
├── IncidentApp.Domain/           # Domain entities
├── IncidentApp.Infrastructure/   # EF Core, services, DI
└── real-time-incident-frontend/
    └── incident-app/             # Angular 17 SPA
```

---

## Backend Setup

### 1. Configure `appsettings.json`

Edit `real-time-incident-backend/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=incidentapp;Username=postgres;Password=YOUR_PASSWORD"
  },
  "Jwt": {
    "Key": "YOUR_SECRET_KEY_AT_LEAST_32_CHARACTERS_LONG",
    "Issuer": "IncidentApp",
    "Audience": "IncidentAppUsers"
  },
  "AllowedOrigins": "http://localhost:4200"
}
```

> **Never commit real secrets.** Use `dotnet user-secrets` or environment variables in production.

### 2. Run Database Migrations

```bash
cd real-time-incident-backend
dotnet ef database update --project ../IncidentApp.Infrastructure/IncidentApp.Infrastructure.csproj --startup-project .
```

### 3. Start the API

```bash
cd real-time-incident-backend
dotnet run
```

API runs at `http://localhost:5000`. Swagger UI at `http://localhost:5000/swagger`.

---

## Frontend Setup

```bash
cd real-time-incident-frontend/incident-app
npm install
npx ng serve
```

App runs at `http://localhost:4200`.

---

## Feature Overview (Phase 1)

| Feature | Status |
|---|---|
| JWT Register / Login / Refresh / Logout | Done |
| Create incident rooms (Admin only) | Done |
| Join rooms | Done |
| List & view rooms | Done |
| Room status updates | Done |
| Live chat via SignalR | Done |
| Chat history (paginated) | Done |
| Online presence (who is in room) | Done |
| Typing indicators | Done |
| Activity log (REST) | Done |
| Angular SPA with routing & auth guard | Done |
| JWT interceptor with auto-refresh | Done |

---

## API Quick Reference

### Auth
```
POST /api/auth/register   { email, displayName, password }
POST /api/auth/login      { email, password }
POST /api/auth/refresh    { refreshToken }
POST /api/auth/logout     { refreshToken }
GET  /api/auth/me
```

### Rooms
```
GET  /api/rooms
POST /api/rooms            { title, description, severity }   [Admin]
GET  /api/rooms/{id}
PUT  /api/rooms/{id}/status { status }                        [Admin/Responder]
POST /api/rooms/{id}/join
GET  /api/rooms/{id}/members
GET  /api/rooms/{id}/messages?page=1&pageSize=50
GET  /api/rooms/{id}/activity?page=1
```

### SignalR Hub: `/hubs/incident`
> Pass JWT via query string: `?access_token=<token>`

**Client → Server**
```
JoinRoom(roomId)
LeaveRoom(roomId)
SendMessage(roomId, content, clientMessageId)
StartTyping(roomId)
StopTyping(roomId)
```

**Server → Client**
```
MessageReceived   { id, roomId, senderId, displayName, content, messageType, sentAt }
UserJoined        { userId, displayName }
UserLeft          { userId, displayName }
UserTyping        { userId, displayName }
UserStoppedTyping { userId }
PresenceUpdated   { onlineUsers: [{ userId, displayName }] }
RoomStatusChanged { roomId, newStatus, changedBy }
Error             string
```

---

## Roles

- **Admin** — can create rooms, change status, assign roles
- **Responder** — can send messages, update task status (Phase 2)
- **Viewer** — read-only

> Roles are per-room. A user registers as `responder` by default. Promote in DB to `admin` manually for now (role management UI is Phase 2).

---

## Next: Phase 2

- Task board (CRUD + SignalR live updates)
- Kanban UI in Angular
- Role management
- Toast notifications
- Read receipts
