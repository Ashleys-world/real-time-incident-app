import { Injectable, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';
import { ChatMessage, OnlineUser, Task, ActivityEntry } from '../models/models';

export interface TypingEvent { userId: string; displayName: string; }
export interface StatusChangedEvent { roomId: string; newStatus: string; changedBy: { userId: string; displayName: string }; }

@Injectable({ providedIn: 'root' })
export class SignalRService implements OnDestroy {
  private connection: signalR.HubConnection | null = null;

  // ── Observables ─────────────────────────────────────────────────────────
  readonly messageReceived$ = new Subject<ChatMessage>();
  readonly userJoined$ = new Subject<{ userId: string; displayName: string }>();
  readonly userLeft$ = new Subject<{ userId: string; displayName: string }>();
  readonly userTyping$ = new Subject<TypingEvent>();
  readonly userStoppedTyping$ = new Subject<{ userId: string }>();
  readonly presenceUpdated$ = new Subject<{ onlineUsers: OnlineUser[] }>();
  readonly roomStatusChanged$ = new Subject<StatusChangedEvent>();
  readonly taskUpdated$ = new Subject<Task>();
  readonly taskDeleted$ = new Subject<{ taskId: string }>();
  readonly activityLogged$ = new Subject<ActivityEntry>();
  readonly error$ = new Subject<string>();

  constructor(private auth: AuthService) {}

  async connect(): Promise<void> {
    if (this.connection) return;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(environment.hubUrl, {
        accessTokenFactory: () => this.auth.getAccessToken() ?? ''
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.registerHandlers();

    await this.connection.start();
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
    }
  }

  async joinRoom(roomId: string): Promise<void> {
    await this.connection?.invoke('JoinRoom', roomId);
  }

  async leaveRoom(roomId: string): Promise<void> {
    await this.connection?.invoke('LeaveRoom', roomId);
  }

  async sendMessage(roomId: string, content: string): Promise<void> {
    const clientMessageId = crypto.randomUUID();
    await this.connection?.invoke('SendMessage', roomId, content, clientMessageId);
  }

  async startTyping(roomId: string): Promise<void> {
    await this.connection?.invoke('StartTyping', roomId);
  }

  async stopTyping(roomId: string): Promise<void> {
    await this.connection?.invoke('StopTyping', roomId);
  }

  async updateTaskStatus(roomId: string, taskId: string, newStatus: string): Promise<void> {
    await this.connection?.invoke('UpdateTaskStatus', roomId, taskId, newStatus);
  }

  private registerHandlers(): void {
    if (!this.connection) return;

    this.connection.on('MessageReceived', (msg: ChatMessage) =>
      this.messageReceived$.next(msg));

    this.connection.on('UserJoined', (evt: { userId: string; displayName: string }) =>
      this.userJoined$.next(evt));

    this.connection.on('UserLeft', (evt: { userId: string; displayName: string }) =>
      this.userLeft$.next(evt));

    this.connection.on('UserTyping', (evt: TypingEvent) =>
      this.userTyping$.next(evt));

    this.connection.on('UserStoppedTyping', (evt: { userId: string }) =>
      this.userStoppedTyping$.next(evt));

    this.connection.on('PresenceUpdated', (evt: { onlineUsers: OnlineUser[] }) =>
      this.presenceUpdated$.next(evt));

    this.connection.on('RoomStatusChanged', (evt: StatusChangedEvent) =>
      this.roomStatusChanged$.next(evt));

    this.connection.on('TaskUpdated', (task: Task) =>
      this.taskUpdated$.next(task));

    this.connection.on('TaskDeleted', (evt: { taskId: string }) =>
      this.taskDeleted$.next(evt));

    this.connection.on('ActivityLogged', (entry: ActivityEntry) =>
      this.activityLogged$.next(entry));

    this.connection.on('Error', (msg: string) =>
      this.error$.next(msg));
  }

  ngOnDestroy(): void {
    this.disconnect();
  }
}
