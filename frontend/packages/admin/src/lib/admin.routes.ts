import { Routes } from '@angular/router';
import { adminGuard } from '@admin/guards/admin.guard';

export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    // Guarding the shell covers every section, so a new tab cannot ship unprotected by
    // forgetting to repeat canActivate on its route.
    canActivate: [adminGuard],
    loadComponent: () => import('./pages/admin-shell/admin-shell').then((m) => m.AdminShell),
    // Same order as the tabs in AdminShell: settings first, then maintenance. A path
    // matches its tab's label, so a URL someone pastes says what it opens.
    children: [
      {
        path: 'accounts',
        loadComponent: () =>
          import('./pages/account-settings/account-settings').then((m) => m.AccountSettingsScreen),
      },
      {
        path: 'scanning',
        loadComponent: () =>
          import('./pages/scan-settings/scan-settings').then((m) => m.ScanSettingsScreen),
      },
      {
        path: 'photos',
        loadComponent: () =>
          import('./pages/photo-cleanup/photo-cleanup').then((m) => m.PhotoCleanup),
      },
      {
        path: 'backup',
        loadComponent: () => import('./pages/backup/backup').then((m) => m.BackupScreen),
      },
      { path: '', pathMatch: 'full', redirectTo: 'accounts' },
      // Accounts lived here until the paths were made to match their labels. A bookmark
      // costs nothing to honour.
      { path: 'settings', pathMatch: 'full', redirectTo: 'accounts' },
    ],
  },
];
