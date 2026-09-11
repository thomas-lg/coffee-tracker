import { Routes } from '@angular/router';
import { adminGuard } from './admin.guard';

export const ADMIN_ROUTES: Routes = [
  {
    path: 'photos',
    canActivate: [adminGuard],
    loadComponent: () => import('./components/photo-cleanup').then((m) => m.PhotoCleanup),
  },
  {
    path: 'settings',
    canActivate: [adminGuard],
    loadComponent: () => import('./components/account-settings').then((m) => m.AccountSettingsScreen),
  },
  { path: '', pathMatch: 'full', redirectTo: 'photos' },
];
