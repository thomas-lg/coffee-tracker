import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import { provideZonelessChangeDetection, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  provideRouter,
  UrlTree,
  type ActivatedRouteSnapshot,
  type RouterStateSnapshot,
} from '@angular/router';
import { AuthStore } from '@coffee-tracker/auth';
import { adminGuard } from './admin.guard';

describe('adminGuard', () => {
  const isAdmin = signal(false);

  beforeEach(() => {
    isAdmin.set(false);
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: AuthStore, useValue: { isAdmin } },
      ],
    });
  });

  afterEach(() => TestBed.resetTestingModule());

  const run = () =>
    TestBed.runInInjectionContext(() =>
      adminGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    );

  it('allows admins through', () => {
    isAdmin.set(true);
    expect(run()).toBe(true);
  });

  it('redirects non-admins to the home page', () => {
    const result = run();
    expect(result).toBeInstanceOf(UrlTree);
    expect((result as UrlTree).toString()).toBe('/');
  });
});
