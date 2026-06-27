import { Routes } from '@angular/router';
import { authGuard } from '@coffee-tracker/auth';

/**
 * The whole API is auth-gated, so only /login and /register are public; everything
 * else sits behind authGuard. Feature areas are lazy-loaded.
 */
export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('@coffee-tracker/auth').then((m) => m.Login),
  },
  {
    path: 'register',
    loadComponent: () => import('@coffee-tracker/auth').then((m) => m.Register),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./home/home').then((m) => m.Home),
  },
  {
    path: 'coffees',
    canActivate: [authGuard],
    loadChildren: () => import('@coffee-tracker/coffees').then((m) => m.COFFEES_ROUTES),
  },
  { path: '**', redirectTo: '' },
];
