import {
  Component, OnInit, OnDestroy, signal, ViewChild, ElementRef, AfterViewChecked
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { RoomService } from '../../services/room.service';
import { SignalRService } from '../../services/signalr.service';
import { AuthService } from '../../services/auth.service';
import { ChatMessage, OnlineUser, RoomDetail } from '../../models/models';

@Component({
  selector: 'app-room-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './room-detail.component.html',
  styleUrls: ['./room-detail.component.scss']
})
export class RoomDetailComponent implements OnInit, OnDestroy, AfterViewChecked {
  @ViewChild('chatEnd') private chatEnd!: ElementRef;

  roomId = '';
  room = signal<RoomDetail | null>(null);
  messages = signal<ChatMessage[]>([]);
  onlineUsers = signal<OnlineUser[]>([]);
  typingUsers = signal<string[]>([]);
  messageInput = '';
  error = signal('');
  loading = signal(true);

  private subs: Subscription[] = [];
  private typingTimers = new Map<string, ReturnType<typeof setTimeout>>();
  private shouldScroll = false;
  private typingDebounce: ReturnType<typeof setTimeout> | null = null;

  readonly currentUserId = this.auth.currentUser()?.id ?? '';

  constructor(
    private route: ActivatedRoute,
    private roomService: RoomService,
    public signalR: SignalRService,
    public auth: AuthService
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

    // Connect and join room via SignalR
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
    if (this.shouldScroll) {
      this.scrollToBottom();
      this.shouldScroll = false;
    }
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
