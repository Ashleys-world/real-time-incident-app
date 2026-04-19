import {
  Component, OnInit, OnDestroy, signal, ViewChild, ElementRef,
  AfterViewChecked, computed
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { RoomService } from '../../services/room.service';
import { SignalRService } from '../../services/signalr.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../services/toast.service';
import { TaskBoardComponent } from '../task-board/task-board.component';
import { ChatMessage, OnlineUser, RoomDetail, Task, ActivityEntry } from '../../models/models';

type Tab = 'chat' | 'tasks' | 'activity';

@Component({
  selector: 'app-room-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, TaskBoardComponent],
  templateUrl: './room-detail.component.html',
  styleUrls: ['./room-detail.component.scss']
})
export class RoomDetailComponent implements OnInit, OnDestroy, AfterViewChecked {
  @ViewChild('chatEnd') private chatEnd!: ElementRef;
  @ViewChild('taskBoard') taskBoard!: TaskBoardComponent;

  roomId = '';
  room = signal<RoomDetail | null>(null);
  messages = signal<ChatMessage[]>([]);
  onlineUsers = signal<OnlineUser[]>([]);
  typingUsers = signal<string[]>([]);
  tasks = signal<Task[]>([]);
  activity = signal<ActivityEntry[]>([]);
  messageInput = '';
  error = signal('');
  loading = signal(true);
  activeTab = signal<Tab>('chat');

  readonly canEdit = computed(() => {
    const room = this.room();
    if (!room) return false;
    const member = room.members.find(m => m.userId === this.currentUserId);
    return member?.role === 'admin' || member?.role === 'responder';
  });

  private subs: Subscription[] = [];
  private typingTimers = new Map<string, ReturnType<typeof setTimeout>>();
  private shouldScroll = false;
  private typingDebounce: ReturnType<typeof setTimeout> | null = null;

  readonly currentUserId = this.auth.currentUser()?.id ?? '';

  constructor(
    private route: ActivatedRoute,
    private roomService: RoomService,
    public signalR: SignalRService,
    public auth: AuthService,
    private toast: ToastService
  ) {}

  async ngOnInit(): Promise<void> {
    this.roomId = this.route.snapshot.paramMap.get('id')!;

    // Load room detail and history in parallel
    this.roomService.getRoom(this.roomId).subscribe({
      next: room => { this.room.set(room); this.loading.set(false); },
      error: () => { this.error.set('Could not load room.'); this.loading.set(false); }
    });

    this.roomService.getMessages(this.roomId).subscribe({
      next: msgs => { this.messages.set(msgs); this.shouldScroll = true; }
    });

    this.roomService.getTasks(this.roomId).subscribe({
      next: tasks => this.tasks.set(tasks)
    });

    this.roomService.getActivity(this.roomId).subscribe({
      next: entries => this.activity.set(entries)
    });

    await this.signalR.connect();
    await this.signalR.joinRoom(this.roomId);

    this.subs.push(
      this.signalR.messageReceived$.subscribe(msg => {
        this.messages.update(m => [...m, msg]);
        this.shouldScroll = true;
      }),

      this.signalR.presenceUpdated$.subscribe(evt =>
        this.onlineUsers.set(evt.onlineUsers)),

      this.signalR.userTyping$.subscribe(evt => {
        if (evt.userId === this.currentUserId) return;
        this.typingUsers.update(u => u.includes(evt.displayName) ? u : [...u, evt.displayName]);
        // Auto-clear after 4s
        clearTimeout(this.typingTimers.get(evt.userId));
        this.typingTimers.set(evt.userId, setTimeout(() =>
          this.typingUsers.update(u => u.filter(n => n !== evt.displayName)), 4000));
      }),

      this.signalR.userStoppedTyping$.subscribe(evt => {
        clearTimeout(this.typingTimers.get(evt.userId));
        this.typingTimers.delete(evt.userId);
        // We don't have displayName here, rely on timer above
      }),

      this.signalR.roomStatusChanged$.subscribe(evt => {
        this.room.update(r => r ? { ...r, status: evt.newStatus } : r);
        this.toast.info(`Room status changed to ${evt.newStatus}`);
      }),

      this.signalR.taskUpdated$.subscribe(task => {
        this.tasks.update(tasks => {
          const idx = tasks.findIndex(t => t.id === task.id);
          if (idx >= 0) { const c = [...tasks]; c[idx] = task; return c; }
          return [...tasks, task];
        });
        this.taskBoard?.applyExternalUpdate(task);
      }),

      this.signalR.taskDeleted$.subscribe(evt => {
        this.tasks.update(tasks => tasks.filter(t => t.id !== evt.taskId));
        this.taskBoard?.applyExternalDelete(evt.taskId);
      }),

      this.signalR.activityLogged$.subscribe(entry => {
        this.activity.update(a => [...a, entry]);
      }),

      this.signalR.error$.subscribe(msg => this.error.set(msg))
    );
  }

  async ngOnDestroy(): Promise<void> {
    await this.signalR.leaveRoom(this.roomId);
    this.subs.forEach(s => s.unsubscribe());
    this.typingTimers.forEach(t => clearTimeout(t));
  }

  ngAfterViewChecked(): void {
    if (this.shouldScroll && this.activeTab() === 'chat') {
      this.scrollToBottom();
      this.shouldScroll = false;
    }
  }

  setTab(tab: Tab): void {
    this.activeTab.set(tab);
    if (tab === 'chat') setTimeout(() => this.scrollToBottom(), 50);
  }

  formatAction(entry: ActivityEntry): string {
    const map: Record<string, string> = {
      'room.status_changed': '🔄 changed room status',
      'task.created':        '✅ created a task',
      'task.status_changed': '🔁 updated task status',
      'task.deleted':        '🗑️ deleted a task',
    };
    return map[entry.action] ?? entry.action;
  }

  async sendMessage(): Promise<void> {
    const content = this.messageInput.trim();
    if (!content) return;
    this.messageInput = '';
    await this.signalR.stopTyping(this.roomId);
    await this.signalR.sendMessage(this.roomId, content);
  }

  onTyping(): void {
    this.signalR.startTyping(this.roomId);
    if (this.typingDebounce) clearTimeout(this.typingDebounce);
    this.typingDebounce = setTimeout(() => this.signalR.stopTyping(this.roomId), 2000);
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.sendMessage();
    }
  }

  isOwnMessage(msg: ChatMessage): boolean {
    return msg.senderId === this.currentUserId;
  }

  private scrollToBottom(): void {
    try {
      this.chatEnd?.nativeElement.scrollIntoView({ behavior: 'smooth' });
    } catch { /* ignore */ }
  }
}
