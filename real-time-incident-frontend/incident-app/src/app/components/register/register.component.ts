import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="auth-container">
      <div class="auth-card">
        <h1>Incident App</h1>
        <h2>Create Account</h2>

        <div class="error-msg" *ngIf="error()">{{ error() }}</div>

        <form (ngSubmit)="onSubmit()">
          <div class="field">
            <label for="displayName">Display Name</label>
            <input id="displayName" type="text" [(ngModel)]="displayName" name="displayName"
                   placeholder="Jane Smith" required />
          </div>

          <div class="field">
            <label for="email">Email</label>
            <input id="email" type="email" [(ngModel)]="email" name="email"
                   placeholder="you@example.com" required autocomplete="email" />
          </div>

          <div class="field">
            <label for="password">Password <span class="hint">(min 8 chars)</span></label>
            <input id="password" type="password" [(ngModel)]="password" name="password"
                   placeholder="••••••••" required minlength="8" autocomplete="new-password" />
          </div>

          <button type="submit" [disabled]="loading()">
            {{ loading() ? 'Creating account...' : 'Register' }}
          </button>
        </form>

        <p class="switch-link">
          Already have an account? <a routerLink="/login">Sign in</a>
        </p>
      </div>
    </div>
  `,
  styleUrls: ['../login/login.component.scss']
})
export class RegisterComponent {
  displayName = '';
  email = '';
  password = '';
  error = signal('');
  loading = signal(false);

  constructor(private auth: AuthService, private router: Router) {}

  onSubmit(): void {
    if (!this.email || !this.password || !this.displayName) return;
    this.loading.set(true);
    this.error.set('');

    this.auth.register(this.email, this.displayName, this.password).subscribe({
      next: () => this.router.navigate(['/rooms']),
      error: err => {
        this.error.set(err.error?.error ?? 'Registration failed. Please try again.');
        this.loading.set(false);
      }
    });
  }
}
