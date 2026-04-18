import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { RoomService } from '../../services/room.service';
import { AuthService } from '../../services/auth.service';
import { Room } from '../../models/models';

const SEVERITY_CLASS: Record<string, string> = {
  P1: 'sev-p1', P2: 'sev-p2', P3: 'sev-p3', P4: 'sev-p4'
};

const STATUS_CLASS: Record<string, string> = {
  Open: 'status-open',
  Investigating: 'status-investigating',
  Mitigating: 'status-mitigating',
  Resolved: 'status-resolved',
  Closed: 'status-closed'
};

@Component({
  selector: 'app-room-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './room-list.component.html',
  styleUrls: ['./room-list.component.scss']
})
export class RoomListComponent implements OnInit {
  rooms = signal<Room[]>([]);
  loading = signal(true);
  error = signal('');
  showCreate = signal(false);

  // Create form
  newTitle = '';
  newDescription = '';
  newSeverity = 'P3';

  readonly isAdmin = computed(() => this.auth.currentUser()?.globalRole === 'admin');

  severityClass = (s: string) => SEVERITY_CLASS[s] ?? '';
  statusClass = (s: string) => STATUS_CLASS[s] ?? '';

  constructor(private rooms_: RoomService, public auth: AuthService) {}

  ngOnInit(): void {
    this.loadRooms();
  }

  loadRooms(): void {
    this.loading.set(true);
    this.rooms_.getRooms().subscribe({
      next: rooms => { this.rooms.set(rooms); this.loading.set(false); },
      error: () => { this.error.set('Failed to load rooms.'); this.loading.set(false); }
    });
  }

  createRoom(): void {
    if (!this.newTitle.trim()) return;
    this.rooms_.createRoom(this.newTitle, this.newDescription, this.newSeverity).subscribe({
      next: room => {
        this.rooms.update(r => [room, ...r]);
        this.showCreate.set(false);
        this.newTitle = '';
        this.newDescription = '';
        this.newSeverity = 'P3';
      },
      error: err => this.error.set(err.error?.error ?? 'Failed to create room.')
    });
  }

  joinRoom(id: string): void {
    this.rooms_.joinRoom(id).subscribe({ error: () => {} });
  }

  logout(): void {
    this.auth.logout();
  }
}
