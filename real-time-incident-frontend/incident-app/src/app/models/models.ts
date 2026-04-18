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
