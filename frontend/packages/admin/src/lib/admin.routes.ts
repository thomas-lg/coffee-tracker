import { Routes } from '@angular/router';
import { adminGuard } from './admin.guard';

export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    // Guarding the shell covers every section, so a new tab cannot ship unprotected by
    // forgetting to repeat canActivate on its route.
    canActivate: [adminGuard],
    loadComponent: () => import('./components/admin-shell').then((m) => m.AdminShell),
    children: [
      {
        path: 'photos',
        loadComponent: () => import('./components/photo-cleanup').then((m) => m.PhotoCleanup),
      },
      {
        path: 'settings',
        loadComponent: () =>
          import('./components/account-settings').then((m) => m.AccountSettingsScreen),
      },
      { path: '', pathMatch: 'full', redirectTo: 'photos' },
    ],
  },
];
