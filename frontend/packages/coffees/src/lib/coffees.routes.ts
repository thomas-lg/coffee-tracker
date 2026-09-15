import { Routes } from '@angular/router';

export const COFFEES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/coffee-grid/coffee-grid').then((m) => m.CoffeeGrid),
  },
  {
    path: 'new',
    loadComponent: () => import('./pages/coffee-form/coffee-form').then((m) => m.CoffeeForm),
  },
  {
    path: ':id',
    loadComponent: () => import('./pages/coffee-detail/coffee-detail').then((m) => m.CoffeeDetail),
  },
  {
    path: ':id/edit',
    loadComponent: () => import('./pages/coffee-form/coffee-form').then((m) => m.CoffeeForm),
  },
];
