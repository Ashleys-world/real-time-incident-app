# Real-Time Incident Coordination App — Requirements

> Version: 1.0  
> Date: 2026-04-18  
> Status: Draft

---

## 1. Project Overview

A real-time incident coordination platform that enables operations teams to manage live situations collaboratively. Teams can open incident rooms, communicate via live chat, assign and track tasks, monitor who is present, and receive instant status updates — all from a single dashboard.

The primary goals are:

- Reduce coordination overhead during live incidents
- Give every team member a shared, real-time view of the incident state
- Maintain a full audit trail (activity feed) of every action taken
- Be deployable to Azure with minimal operational overhead

---

## 2. Tech Stack

| Layer | Technology | Notes |
|---|---|---|
| Frontend | Angular 17+ (standalone components) | Material UI or Tailwind CSS |
| Backend API | ASP.NET Core 8 | REST + SignalR hubs |
| Real-time | SignalR (local → Azure SignalR Service) | Phase 1–3 local, Phase 4 Azure |
| Relational DB | PostgreSQL | Users, roles, rooms, tasks, incidents |
| Document DB | Azure Cosmos DB (NoSQL) | Chat messages, event logs, activity streams (Phase 3+) |
| Auth | ASP.NET Core Identity + JWT | Role-based, short-lived access tokens |
| Hosting | Azure App Service / Azure Container Apps | Phase 4 |
| Async processing | Azure Functions + Azure Service Bus | Notifications (Phase 4) |
| ORM | Entity Framework Core 8 + Npgsql | Code-first migrations |

---

## 3. Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                        Angular SPA                          │
│  Dashboard │ Incident Rooms │ Chat │ Task Board │ Timeline  │
└────────────────────────┬────────────────────────────────────┘
                         │ HTTP (REST) + WebSocket (SignalR)
┌────────────────────────▼────────────────────────────────────┐
│                   ASP.NET Core 8 API                        │
│  Auth Controller │ Incident Controller │ Task Controller     │
│  IncidentHub (SignalR) │ PresenceHub (SignalR)               │
└─────────┬────────────────────────────────────────┬──────────┘
          │                                        │
┌─────────▼──────────┐               ┌─────────────▼──────────┐
│    PostgreSQL       │               │   Azure Cosmos DB       │
│ Users, Roles        │               │ Chat Messages           │
│ Incidents, Rooms    │               │ Activity Logs           │
│ Tasks, Assignments  │               │ Event Streams           │
└────────────────────┘               └────────────────────────┘
```

**Key design decisions:**
- SignalR groups map 1-to-1 with incident rooms (`room:{id}`)
- Presence is tracked in-memory (IMemoryCache / distributed cache) — not persisted
- All SignalR events are also written to the activity log for auditability
- JWT tokens are validated on both REST and SignalR connections

---

## 4. Functional Requirements

### 4.1 User Authentication

| ID | Requirement |
|---|---|
| AUTH-01 | Users can register with email and password |
| AUTH-02 | Users can log in and receive a JWT access token + refresh token |
| AUTH-03 | Tokens expire after 1 hour; refresh tokens after 7 days |
| AUTH-04 | JWT is sent via Authorization header for REST and via query string for SignalR |
| AUTH-05 | Passwords are hashed with bcrypt (via ASP.NET Core Identity) |
| AUTH-06 | Users have a display name, avatar (initials fallback), and a global role |

### 4.2 Incident Rooms

| ID | Requirement |
|---|---|
| ROOM-01 | An admin can create an incident room with a title, severity, and description |
| ROOM-02 | Any authenticated user can join an open incident room |
| ROOM-03 | Rooms have a status: `Open`, `Investigating`, `Mitigating`, `Resolved`, `Closed` |
| ROOM-04 | Only admins and responders can change room status |
| ROOM-05 | Status changes are broadcast in real-time to all room members |
| ROOM-06 | A room can be archived (soft delete) by an admin |
| ROOM-07 | Rooms display a severity level: `P1 Critical`, `P2 High`, `P3 Medium`, `P4 Low` |
| ROOM-08 | Joining/leaving a room is broadcast to all room members |

### 4.3 Live Team Chat

| ID | Requirement |
|---|---|
| CHAT-01 | Users can send text messages within an incident room |
| CHAT-02 | Messages are broadcast to all connected room members in real-time via SignalR |
| CHAT-03 | Messages are persisted (PostgreSQL Phase 1, Cosmos DB Phase 3) |
| CHAT-04 | Chat history is paginated (cursor-based, 50 messages per page) |
| CHAT-05 | Typing indicators are shown to other room members |
| CHAT-06 | Messages include sender, timestamp, and room reference |
| CHAT-07 | System messages (e.g., "User X joined", "Status changed to Resolved") appear in chat |

### 4.4 Presence

| ID | Requirement |
|---|---|
| PRES-01 | When a user connects to a room, they appear in the "Online" list |
| PRES-02 | When a user disconnects, they are removed from the list after a grace period (30s) |
| PRES-03 | Typing state is broadcast and cleared after 3 seconds of inactivity |
| PRES-04 | Presence data is held in a distributed/in-memory cache, not the database |
| PRES-05 | A user can be in multiple rooms simultaneously |

### 4.5 Task Board

| ID | Requirement |
|---|---|
| TASK-01 | Admins and responders can create tasks within an incident room |
| TASK-02 | Tasks have: title, description, assignee, status, priority, due date |
| TASK-03 | Task statuses: `Todo`, `InProgress`, `Blocked`, `Done` |
| TASK-04 | Task updates (status change, reassignment) are broadcast in real-time |
| TASK-05 | Viewers can view tasks but cannot create or modify them |
| TASK-06 | Tasks are displayed in a Kanban-style board on the frontend |

### 4.6 Activity Feed / Timeline

| ID | Requirement |
|---|---|
| FEED-01 | Every significant action generates an activity log entry |
| FEED-02 | Logged actions include: room created, status changed, user joined/left, task created/updated, message sent (summary only) |
| FEED-03 | Activity feed is displayed chronologically in the room view |
| FEED-04 | Activity entries include: actor, action, target, timestamp |
| FEED-05 | Feed is paginated and loaded on scroll |

### 4.7 Notifications

| ID | Requirement |
|---|---|
| NOTIF-01 | Toast notifications are shown in the UI for relevant events (task assigned, status changed) |
| NOTIF-02 | Users can configure which notification types they receive |
| NOTIF-03 | (Phase 4) Email notifications via Azure Functions + SendGrid |
| NOTIF-04 | (Phase 4) Push notifications via Azure Notification Hubs |

### 4.8 Role-Based Permissions

| Feature | Admin | Responder | Viewer |
|---|---|---|---|
| Create incident room | ✅ | ❌ | ❌ |
| Change room status | ✅ | ✅ | ❌ |
| Archive/close room | ✅ | ❌ | ❌ |
| Send chat messages | ✅ | ✅ | ❌ |
| Create tasks | ✅ | ✅ | ❌ |
| Update task status | ✅ | ✅ | ❌ |
| View all data | ✅ | ✅ | ✅ |
| Assign roles | ✅ | ❌ | ❌ |
| Invite users to room | ✅ | ✅ | ❌ |

> Roles are scoped per room. A user can be an Admin in one room and a Viewer in another.

---

## 5. Non-Functional Requirements

| ID | Requirement |
|---|---|
| NFR-01 | SignalR messages must be delivered within 500ms under normal load |
| NFR-02 | API endpoints must respond within 200ms (p95) excluding database cold start |
| NFR-03 | The system must support at least 100 concurrent room connections per instance |
| NFR-04 | JWT tokens must be validated on every SignalR connection and REST request |
| NFR-05 | All database writes must be idempotent where applicable (Phase 3) |
| NFR-06 | Application must handle SignalR reconnection gracefully (Phase 3) |
| NFR-07 | Sensitive config (DB strings, JWT secret) stored in environment variables / Azure Key Vault |
| NFR-08 | CORS is locked to known frontend origin(s) only |
| NFR-09 | All API inputs are validated with FluentValidation or Data Annotations |
| NFR-10 | Logging via Serilog, structured JSON, shipped to Azure Application Insights (Phase 4) |

---

## 6. Phase Breakdown

### Phase 1 — Basic Real-Time (Foundation)
**Goal:** Working end-to-end real-time chat in a room with presence.

- [ ] Project scaffold: ASP.NET Core 8 backend + Angular frontend
- [ ] PostgreSQL schema: users, rooms, messages, room_members
- [ ] JWT authentication (register, login, refresh)
- [ ] REST API: create room, join room, list rooms, get room detail
- [ ] SignalR `IncidentHub`: join room group, send message, broadcast message
- [ ] Presence: online user list per room, join/leave events
- [ ] Angular: login page, room list, room chat view, online user list

### Phase 2 — Collaborative Updates
**Goal:** Full task board with live updates and richer presence.

- [ ] PostgreSQL schema: tasks, assignments, activity_log
- [ ] REST API: CRUD for tasks, assign task, update status
- [ ] SignalR: broadcast task created/updated/assigned events
- [ ] Typing indicators (start typing / stop typing hub methods)
- [ ] Read receipts (last-seen message per user)
- [ ] Role-based permissions enforced in backend
- [ ] Angular: Kanban task board with real-time updates, typing indicator UI
- [ ] Toast notification system in Angular
- [ ] Activity feed in room view

### Phase 3 — Advanced Real-World Behavior
**Goal:** Production-quality resilience, history recovery, idempotency.

- [ ] SignalR reconnection handling (automatic reconnect + state recovery)
- [ ] Missed message recovery: fetch messages since last seen on reconnect
- [ ] Cursor-based pagination for chat history
- [ ] Idempotent writes (client-generated message IDs)
- [ ] Event timestamp ordering guarantees
- [ ] Migrate chat messages and activity logs to Azure Cosmos DB
- [ ] Distributed cache (Redis) for presence state
- [ ] Angular: scroll-based history loading, reconnect status indicator

### Phase 4 — Standout Features
**Goal:** Production deployment and advanced features for CV impact.

- [ ] Azure SignalR Service integration (replace local SignalR hub backplane)
- [ ] Deploy backend to Azure App Service / Container Apps
- [ ] Deploy frontend to Azure Static Web Apps
- [ ] Azure Functions for async email notifications (SendGrid)
- [ ] Azure Service Bus for event-driven processing
- [ ] Live incident dashboard (metrics, active rooms, open tasks count)
- [ ] Incident heatmap (incident frequency by time/type)
- [ ] File attachments (Azure Blob Storage)
- [ ] Analytics: response time tracking, MTTD/MTTR metrics
- [ ] Azure Application Insights for logging and telemetry

---

## 7. Data Models

### Users
```sql
users (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  email       VARCHAR(255) UNIQUE NOT NULL,
  display_name VARCHAR(100) NOT NULL,
  password_hash TEXT NOT NULL,
  global_role  VARCHAR(20) NOT NULL DEFAULT 'responder', -- 'admin' | 'responder' | 'viewer'
  created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
)
```

### Incidents / Rooms
```sql
incident_rooms (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  title       VARCHAR(255) NOT NULL,
  description TEXT,
  severity    VARCHAR(20) NOT NULL,   -- 'P1' | 'P2' | 'P3' | 'P4'
  status      VARCHAR(30) NOT NULL DEFAULT 'Open',
  created_by  UUID NOT NULL REFERENCES users(id),
  created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  archived_at TIMESTAMPTZ
)
```

### Room Members (with per-room role)
```sql
room_members (
  room_id  UUID NOT NULL REFERENCES incident_rooms(id),
  user_id  UUID NOT NULL REFERENCES users(id),
  role     VARCHAR(20) NOT NULL DEFAULT 'responder', -- per-room role
  joined_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  PRIMARY KEY (room_id, user_id)
)
```

### Messages (Phase 1 PostgreSQL, Phase 3 migrate to Cosmos DB)
```sql
messages (
  id          UUID PRIMARY KEY,   -- client-generated for idempotency
  room_id     UUID NOT NULL REFERENCES incident_rooms(id),
  sender_id   UUID NOT NULL REFERENCES users(id),
  content     TEXT NOT NULL,
  message_type VARCHAR(20) NOT NULL DEFAULT 'user', -- 'user' | 'system'
  sent_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
)
```

### Tasks
```sql
tasks (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  room_id     UUID NOT NULL REFERENCES incident_rooms(id),
  title       VARCHAR(255) NOT NULL,
  description TEXT,
  status      VARCHAR(20) NOT NULL DEFAULT 'Todo', -- 'Todo' | 'InProgress' | 'Blocked' | 'Done'
  priority    VARCHAR(20) NOT NULL DEFAULT 'Medium',
  assignee_id UUID REFERENCES users(id),
  created_by  UUID NOT NULL REFERENCES users(id),
  due_at      TIMESTAMPTZ,
  created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
)
```

### Activity Log
```sql
activity_log (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  room_id     UUID NOT NULL REFERENCES incident_rooms(id),
  actor_id    UUID NOT NULL REFERENCES users(id),
  action      VARCHAR(100) NOT NULL,  -- e.g. 'task.created', 'room.status_changed'
  target_type VARCHAR(50),            -- 'task' | 'room' | 'message'
  target_id   UUID,
  payload     JSONB,                  -- diff or metadata
  occurred_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
)
```

---

## 8. API Endpoints

### Auth
| Method | Path | Description |
|---|---|---|
| POST | `/api/auth/register` | Register new user |
| POST | `/api/auth/login` | Login, returns JWT + refresh token |
| POST | `/api/auth/refresh` | Exchange refresh token for new access token |
| POST | `/api/auth/logout` | Revoke refresh token |

### Rooms
| Method | Path | Description |
|---|---|---|
| GET | `/api/rooms` | List all open rooms |
| POST | `/api/rooms` | Create incident room (Admin only) |
| GET | `/api/rooms/{id}` | Get room detail |
| PUT | `/api/rooms/{id}/status` | Update room status (Admin/Responder) |
| POST | `/api/rooms/{id}/join` | Join room |
| GET | `/api/rooms/{id}/members` | List room members |

### Messages
| Method | Path | Description |
|---|---|---|
| GET | `/api/rooms/{id}/messages` | Get paginated message history |

### Tasks
| Method | Path | Description |
|---|---|---|
| GET | `/api/rooms/{id}/tasks` | List tasks in room |
| POST | `/api/rooms/{id}/tasks` | Create task (Admin/Responder) |
| PUT | `/api/rooms/{id}/tasks/{taskId}` | Update task |
| DELETE | `/api/rooms/{id}/tasks/{taskId}` | Delete task (Admin only) |

### Activity
| Method | Path | Description |
|---|---|---|
| GET | `/api/rooms/{id}/activity` | Get paginated activity feed |

---

## 9. SignalR Hub Methods

### `IncidentHub` (`/hubs/incident`)

**Client → Server (invocations):**
| Method | Parameters | Description |
|---|---|---|
| `JoinRoom` | `roomId: string` | Join SignalR group for room |
| `LeaveRoom` | `roomId: string` | Leave SignalR group |
| `SendMessage` | `roomId, content, clientMessageId` | Broadcast chat message |
| `StartTyping` | `roomId: string` | Notify room user is typing |
| `StopTyping` | `roomId: string` | Notify room user stopped typing |
| `UpdateTaskStatus` | `roomId, taskId, newStatus` | Update task and broadcast |

**Server → Client (broadcasts):**
| Event | Payload | Description |
|---|---|---|
| `MessageReceived` | `{ id, senderId, displayName, content, sentAt }` | New chat message |
| `UserJoined` | `{ userId, displayName }` | User joined room |
| `UserLeft` | `{ userId, displayName }` | User left room |
| `UserTyping` | `{ userId, displayName }` | User is typing |
| `UserStoppedTyping` | `{ userId }` | User stopped typing |
| `TaskUpdated` | `{ task }` | Task created/updated |
| `RoomStatusChanged` | `{ roomId, newStatus, changedBy }` | Room status change |
| `ActivityLogged` | `{ entry }` | New activity feed entry |
| `PresenceUpdated` | `{ onlineUsers: [] }` | Full online user list for room |

---

## 10. Out of Scope (Phase 1 and Phase 2)

- Azure deployment infrastructure (Phase 4)
- Email and push notifications (Phase 4)
- File attachments (Phase 4)
- Analytics and heatmaps (Phase 4)
- Cosmos DB migration (Phase 3)
- Redis distributed cache (Phase 3)
- Azure SignalR Service backplane (Phase 4)
- Mobile clients
- SSO / OAuth2 / external identity providers
- Multi-tenancy / organization management
- SLA tracking or automated escalation

---

## 11. Definition of Done (per Phase)

A phase is considered complete when:
1. All listed features are implemented and manually tested end-to-end
2. Unit tests cover core business logic (services, validators)
3. Integration tests cover critical API endpoints
4. SignalR hub methods are tested via a test client or Postman
5. No known P1/P2 bugs outstanding
6. README is updated with setup and run instructions for that phase

ashley [ ~ ]$ bash /home/ashley/provision.sh
Generated JWT secret (save this!): sJ29jciptJyTl5//DKD2rC3klJmiy6ZKYPQVruziAXZLplTLTYSM6VjeE4IlJdGU