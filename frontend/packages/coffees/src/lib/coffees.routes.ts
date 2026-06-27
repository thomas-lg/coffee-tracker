import { Routes } from '@angular/router';

export const COFFEES_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./coffee-grid').then((m) => m.CoffeeGrid) },
  { path: 'new', loadComponent: () => import('./coffee-form').then((m) => m.CoffeeForm) },
  { path: ':id', loadComponent: () => import('./coffee-detail').then((m) => m.CoffeeDetail) },
  { path: ':id/edit', loadComponent: () => import('./coffee-form').then((m) => m.CoffeeForm) },
];
