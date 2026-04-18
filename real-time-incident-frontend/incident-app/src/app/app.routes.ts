import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'rooms', pathMatch: 'full' },
  {
    path: 'login',
    loadComponent: () =>
      import('./components/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'register',
    loadComponent: () =>
      import('./components/register/register.component').then(m => m.RegisterComponent)
  },
  {
    path: 'rooms',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./components/room-list/room-list.component').then(m => m.RoomListComponent)
  },
  {
    path: 'rooms/:id',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./components/room-detail/room-detail.component').then(m => m.RoomDetailComponent)
  },
  { path: '**', redirectTo: 'rooms' }
];
