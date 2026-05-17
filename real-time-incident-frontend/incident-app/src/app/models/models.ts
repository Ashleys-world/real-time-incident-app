export interface User {
  id: string;
  email: string;
  displayName: string;
  globalRole: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  user: User;
}

export interface Room {
  id: string;
  title: string;
  description?: string;
  severity: string;
  status: string;
  createdById: string;
  createdAt: string;
  updatedAt: string;
  memberCount: number;
}

export interface RoomMember {
  userId: string;
  displayName: string;
  email: string;
  role: string;
  joinedAt: string;
}

export interface RoomDetail extends Room {
  members: RoomMember[];
}

export interface ChatMessage {
  id: string;
  roomId: string;
  senderId: string;
  senderDisplayName: string;
  content: string;
  messageType: 'user' | 'system';
  sentAt: string;
}

export interface OnlineUser {
  userId: string;
  displayName: string;
}

export interface ActivityEntry {
  id: string;
  roomId: string;
  actorId: string;
  actorDisplayName: string;
  action: string;
  targetType?: string;
  targetId?: string;
  payload?: string;
  occurredAt: string;
}

export interface Task {
  id: string;
  roomId: string;
  title: string;
  description?: string;
  status: 'Todo' | 'InProgress' | 'Blocked' | 'Done';
  priority: 'Low' | 'Medium' | 'High' | 'Critical';
  assigneeId?: string;
  assigneeDisplayName?: string;
  createdById: string;
  createdByDisplayName: string;
  dueAt?: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateTaskRequest {
  title: string;
  description?: string;
  priority: string;
  assigneeId?: string;
  dueAt?: string;
}

export interface UpdateTaskRequest {
  title?: string;
  description?: string;
  status?: string;
  priority?: string;
  assigneeId?: string;
  dueAt?: string;
}

export interface Toast {
  id: string;
  message: string;
  type: 'success' | 'error' | 'info' | 'warning';
}
