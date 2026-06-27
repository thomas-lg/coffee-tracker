import { Routes } from '@angular/router';

export const COFFEES_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./components/coffee-grid').then((m) => m.CoffeeGrid) },
  { path: 'new', loadComponent: () => import('./components/coffee-form').then((m) => m.CoffeeForm) },
  { path: ':id', loadComponent: () => import('./components/coffee-detail').then((m) => m.CoffeeDetail) },
  { path: ':id/edit', loadComponent: () => import('./components/coffee-form').then((m) => m.CoffeeForm) },
];
