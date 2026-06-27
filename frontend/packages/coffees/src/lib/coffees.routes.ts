import { Routes } from '@angular/router';

export const COFFEES_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./coffee-grid').then((m) => m.CoffeeGrid) },
];
