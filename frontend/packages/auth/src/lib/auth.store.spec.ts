import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AuthApi, type AuthResponse, type Login } from '@coffee-tracker/data';
import { AuthStore } from './auth.store';

const STORAGE_KEY = 'ct.session';

const futureIso = (msAhead = 3_600_000) => new Date(Date.now() + msAhead).toISOString();
const pastIso = () => new Date(Date.now() - 1_000).toISOString();

describe('AuthStore', () => {
  let response: AuthResponse;

  beforeEach(() => {
    localStorage.clear();
    response = { token: 'tok', userId: 'u1', displayName: 'Ada', isAdmin: true, expiresAt: futureIso() };

    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        // AuthStore only depends on AuthApi; stub it so no HTTP is needed.
        { provide: AuthApi, useValue: { login: () => of(response), register: () => of(response) } },
      ],
    });
  });

  afterEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
  });

  it('starts unauthenticated when nothing is stored', () => {
    const store = TestBed.inject(AuthStore);
    expect(store.session()).toBeNull();
    expect(store.isAuthenticated()).toBe(false);
    expect(store.token()).toBeNull();
  });

  it('restores a valid (non-expired) stored session', () => {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ token: 't', userId: 'u', displayName: 'Grace', isAdmin: true, expiresAt: futureIso() }),
    );

    const store = TestBed.inject(AuthStore);

    expect(store.isAuthenticated()).toBe(true);
    expect(store.token()).toBe('t');
    expect(store.displayName()).toBe('Grace');
    expect(store.isAdmin()).toBe(true);
  });

  it('discards an expired stored session and removes the key', () => {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ token: 't', userId: 'u', displayName: null, isAdmin: false, expiresAt: pastIso() }),
    );

    const store = TestBed.inject(AuthStore);

    expect(store.session()).toBeNull();
    expect(store.isAuthenticated()).toBe(false);
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });

  it('ignores a corrupt stored session without throwing', () => {
    localStorage.setItem(STORAGE_KEY, 'not json{');

    const store = TestBed.inject(AuthStore);

    expect(store.session()).toBeNull();
  });

  it('persists the session on login', async () => {
    const store = TestBed.inject(AuthStore);

    await store.login({ email: 'a@b.c', password: 'secret-123' } satisfies Login);

    expect(store.isAuthenticated()).toBe(true);
    expect(store.token()).toBe('tok');
    expect(store.isAdmin()).toBe(true);
    expect(JSON.parse(localStorage.getItem(STORAGE_KEY)!).userId).toBe('u1');
  });

  it('clears the session and storage on logout', async () => {
    const store = TestBed.inject(AuthStore);
    await store.login({ email: 'a@b.c', password: 'secret-123' } satisfies Login);

    store.logout();

    expect(store.session()).toBeNull();
    expect(store.isAuthenticated()).toBe(false);
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });
});
