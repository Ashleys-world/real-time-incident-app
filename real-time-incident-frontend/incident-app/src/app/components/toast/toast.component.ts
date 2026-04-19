import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService } from '../../services/toast.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="toast-container">
      <div *ngFor="let toast of toastService.toasts()" class="toast" [ngClass]="'toast--' + toast.type">
        <span class="toast__message">{{ toast.message }}</span>
        <button class="toast__close" (click)="toastService.dismiss(toast.id)">&times;</button>
      </div>
    </div>
  `,
  styles: [`
    .toast-container {
      position: fixed;
      bottom: 1.5rem;
      right: 1.5rem;
      display: flex;
      flex-direction: column;
      gap: .5rem;
      z-index: 9999;
    }
    .toast {
      display: flex;
      align-items: center;
      gap: .75rem;
      padding: .75rem 1rem;
      border-radius: 6px;
      min-width: 280px;
      max-width: 420px;
      font-size: .875rem;
      box-shadow: 0 4px 12px rgba(0,0,0,.25);
      animation: slideIn .2s ease;
    }
    @keyframes slideIn {
      from { opacity: 0; transform: translateX(40px); }
      to   { opacity: 1; transform: translateX(0); }
    }
    .toast--success { background: #16a34a; color: #fff; }
    .toast--error   { background: #dc2626; color: #fff; }
    .toast--warning { background: #d97706; color: #fff; }
    .toast--info    { background: #2563eb; color: #fff; }
    .toast__message { flex: 1; }
    .toast__close   { background: none; border: none; color: inherit; cursor: pointer; font-size: 1rem; opacity: .8; }
    .toast__close:hover { opacity: 1; }
  `]
})
export class ToastComponent {
  constructor(public toastService: ToastService) {}
}
