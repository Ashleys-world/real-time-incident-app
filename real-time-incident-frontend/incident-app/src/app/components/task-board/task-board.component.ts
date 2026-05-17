import {
  Component, Input, Output, EventEmitter, signal, computed
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Task, CreateTaskRequest, RoomMember } from '../../models/models';
import { RoomService } from '../../services/room.service';
import { ToastService } from '../../services/toast.service';

const COLUMNS: { status: Task['status']; label: string; color: string }[] = [
  { status: 'Todo',       label: 'To Do',      color: '#64748b' },
  { status: 'InProgress', label: 'In Progress', color: '#2563eb' },
  { status: 'Blocked',    label: 'Blocked',     color: '#dc2626' },
  { status: 'Done',       label: 'Done',        color: '#16a34a' },
];

@Component({
  selector: 'app-task-board',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './task-board.component.html',
  styleUrls: ['./task-board.component.scss']
})
export class TaskBoardComponent {
  @Input() roomId = '';
  @Input() set tasks(value: Task[]) { this._tasks.set(value); }
  @Input() members: RoomMember[] = [];
  @Input() canEdit = false;
  @Output() taskChanged = new EventEmitter<Task>();
  @Output() taskDeleted = new EventEmitter<string>();

  readonly _tasks = signal<Task[]>([]);
  readonly columns = COLUMNS;

  showCreateForm = signal(false);
  newTitle = '';
  newDescription = '';
  newPriority = 'Medium';
  newAssigneeId = '';
  newDueAt = '';
  creating = signal(false);

  readonly priorities = ['Low', 'Medium', 'High', 'Critical'];

  constructor(
    private roomService: RoomService,
    private toast: ToastService
  ) {}

  tasksForColumn(status: Task['status']): Task[] {
    return this._tasks().filter(t => t.status === status);
  }

  priorityClass(priority: string): string {
    return `priority--${priority.toLowerCase()}`;
  }

  moveTask(task: Task, newStatus: Task['status']): void {
    if (!this.canEdit || task.status === newStatus) return;

    this.roomService.updateTask(this.roomId, task.id, { status: newStatus }).subscribe({
      next: updated => {
        this._tasks.update(tasks => tasks.map(t => t.id === updated.id ? updated : t));
        this.taskChanged.emit(updated);
        this.toast.info(`Task moved to ${newStatus}`);
      },
      error: () => this.toast.error('Failed to update task status.')
    });
  }

  createTask(): void {
    if (!this.newTitle.trim()) return;
    this.creating.set(true);

    const request: CreateTaskRequest = {
      title: this.newTitle.trim(),
      description: this.newDescription.trim() || undefined,
      priority: this.newPriority,
      assigneeId: this.newAssigneeId || undefined,
      dueAt: this.newDueAt || undefined
    };

    this.roomService.createTask(this.roomId, request).subscribe({
      next: task => {
        this._tasks.update(t => [...t, task]);
        this.taskChanged.emit(task);
        this.toast.success(`Task "${task.title}" created.`);
        this.resetForm();
      },
      error: () => {
        this.toast.error('Failed to create task.');
        this.creating.set(false);
      }
    });
  }

  deleteTask(task: Task): void {
    if (!confirm(`Delete task "${task.title}"?`)) return;
    this.roomService.deleteTask(this.roomId, task.id).subscribe({
      next: () => {
        this._tasks.update(t => t.filter(x => x.id !== task.id));
        this.taskDeleted.emit(task.id);
        this.toast.success('Task deleted.');
      },
      error: () => this.toast.error('Failed to delete task.')
    });
  }

  applyExternalUpdate(task: Task): void {
    this._tasks.update(tasks => {
      const idx = tasks.findIndex(t => t.id === task.id);
      if (idx >= 0) {
        const copy = [...tasks];
        copy[idx] = task;
        return copy;
      }
      return [...tasks, task];
    });
  }

  applyExternalDelete(taskId: string): void {
    this._tasks.update(tasks => tasks.filter(t => t.id !== taskId));
  }

  private resetForm(): void {
    this.newTitle = '';
    this.newDescription = '';
    this.newPriority = 'Medium';
    this.newAssigneeId = '';
    this.newDueAt = '';
    this.creating.set(false);
    this.showCreateForm.set(false);
  }
}
