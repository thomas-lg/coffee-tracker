import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { ToastService } from '@coffee-tracker/ui';
import { AuthApi, type AuthResponse, type Login } from '@coffee-tracker/data';
import { AuthStore } from './auth.store';

const STORAGE_KEY = 'ct.session';

const futureIso = (msAhead = 3_600_000) => new Date(Date.now() + msAhead).toISOString();
const pastIso = () => new Date(Date.now() - 1_000).toISOString();

function authResponse(overrides: Partial<AuthResponse> = {}): AuthResponse {
  return {
    token: 'tok',
    expiresAt: futureIso(),
    refreshToken: 'refresh-1',
    refreshExpiresAt: futureIso(30 * 24 * 3_600_000),
    userId: 'u1',
    displayName: 'Ada',
    isAdmin: true,
    ...overrides,
  };
}

describe('AuthStore', () => {
  let response: AuthResponse;
  let navigateByUrl: ReturnType<typeof vi.fn>;
  /** Stands in for the browser's current URL so the returnUrl handling can be driven. */
  let routerUrl: string;
  let toast: { show: ReturnType<typeof vi.fn> };
  let api: {
    login: ReturnType<typeof vi.fn>;
    register: ReturnType<typeof vi.fn>;
    refresh: ReturnType<typeof vi.fn>;
    logout: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    localStorage.clear();
    response = authResponse();
    navigateByUrl = vi.fn();
    routerUrl = '/login';
    toast = { show: vi.fn() };
    api = {
      login: vi.fn(() => of(response)),
      register: vi.fn(() => of(response)),
      refresh: vi.fn(() => of(response)),
      logout: vi.fn(() => of(undefined)),
    };

    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        // The store owns its own side effects now: it navigates on sign-in/out and
        // toasts a rejected sign-in, so all three collaborators are stubbed.
        { provide: AuthApi, useValue: api },
        {
          provide: Router,
          useValue: {
            navigateByUrl,
            get url() {
              return routerUrl;
            },
            parseUrl: (url: string) => {
              const query = url.includes('?') ? url.slice(url.indexOf('?') + 1) : '';
              return { queryParams: Object.fromEntries(new URLSearchParams(query)) };
            },
          },
        },
        { provide: ToastService, useValue: toast },
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
      JSON.stringify({
        token: 't',
        userId: 'u',
        displayName: 'Grace',
        isAdmin: true,
        expiresAt: futureIso(),
        refreshToken: 'r',
        refreshExpiresAt: futureIso(),
      }),
    );

    const store = TestBed.inject(AuthStore);

    expect(store.isAuthenticated()).toBe(true);
    expect(store.hasValidAccessToken()).toBe(true);
    expect(store.token()).toBe('t');
    expect(store.displayName()).toBe('Grace');
    expect(store.isAdmin()).toBe(true);
  });

  it('restores a session whose access token expired but whose refresh token is alive', () => {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({
        token: 't',
        userId: 'u',
        displayName: null,
        isAdmin: false,
        expiresAt: pastIso(),
        refreshToken: 'r',
        refreshExpiresAt: futureIso(),
      }),
    );

    const store = TestBed.inject(AuthStore);

    expect(store.session()).not.toBeNull();
    expect(store.hasValidAccessToken()).toBe(false);
    expect(store.canRefresh()).toBe(true);
    expect(store.isAuthenticated()).toBe(true);
  });

  it('discards a fully expired stored session (access + refresh) and removes the key', () => {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({
        token: 't',
        userId: 'u',
        displayName: null,
        isAdmin: false,
        expiresAt: pastIso(),
        refreshToken: 'r',
        refreshExpiresAt: pastIso(),
      }),
    );

    const store = TestBed.inject(AuthStore);

    expect(store.session()).toBeNull();
    expect(store.isAuthenticated()).toBe(false);
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });

  it('discards an expired pre-refresh session (no refresh token stored)', () => {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({
        token: 't',
        userId: 'u',
        displayName: null,
        isAdmin: false,
        expiresAt: pastIso(),
      }),
    );

    const store = TestBed.inject(AuthStore);

    expect(store.session()).toBeNull();
    expect(store.isAuthenticated()).toBe(false);
  });

  it('ignores a corrupt stored session without throwing', () => {
    localStorage.setItem(STORAGE_KEY, 'not json{');

    const store = TestBed.inject(AuthStore);

    expect(store.session()).toBeNull();
  });

  it('persists the full session (incl. refresh pair) on login', async () => {
    const store = TestBed.inject(AuthStore);

    await store.login({ email: 'a@b.c', password: 'secret-123' } satisfies Login);

    expect(store.isAuthenticated()).toBe(true);
    expect(store.token()).toBe('tok');
    expect(store.isAdmin()).toBe(true);
    const stored = JSON.parse(localStorage.getItem(STORAGE_KEY)!);
    expect(stored.userId).toBe('u1');
    expect(stored.refreshToken).toBe('refresh-1');
    expect(stored.refreshExpiresAt).toBe(response.refreshExpiresAt);
  });

  it('ignores a second sign-in while the first is still in flight', () => {
    // The submit button is inert by then, but Enter in a field submits the form without
    // going through it, so the operator is the only guard left.
    api.login.mockReturnValue(new Subject<AuthResponse>());
    const store = TestBed.inject(AuthStore);

    store.login({ email: 'a@b.c', password: 'secret-123' } satisfies Login);
    store.login({ email: 'a@b.c', password: 'secret-123' } satisfies Login);

    expect(api.login).toHaveBeenCalledTimes(1);
  });

  it('ignores a second account creation while the first is still in flight', () => {
    api.register.mockReturnValue(new Subject<AuthResponse>());
    const store = TestBed.inject(AuthStore);

    store.register({ email: 'a@b.c', password: 'secret-123', displayName: 'Ada' });
    store.register({ email: 'a@b.c', password: 'secret-123', displayName: 'Ada' });

    expect(api.register).toHaveBeenCalledTimes(1);
  });

  it('exchanges the refresh token for a rotated pair on refresh()', async () => {
    const store = TestBed.inject(AuthStore);
    await store.login({ email: 'a@b.c', password: 'secret-123' });
    const rotated = authResponse({ token: 'tok-2', refreshToken: 'refresh-2' });
    api.refresh.mockReturnValue(of(rotated));

    await expect(store.refresh()).resolves.toBe(true);

    expect(api.refresh).toHaveBeenCalledWith('refresh-1');
    expect(store.token()).toBe('tok-2');
    expect(JSON.parse(localStorage.getItem(STORAGE_KEY)!).refreshToken).toBe('refresh-2');
  });

  it('shares one in-flight refresh between concurrent callers', async () => {
    const store = TestBed.inject(AuthStore);
    await store.login({ email: 'a@b.c', password: 'secret-123' });

    const [a, b] = await Promise.all([store.refresh(), store.refresh()]);

    expect(a).toBe(true);
    expect(b).toBe(true);
    expect(api.refresh).toHaveBeenCalledTimes(1);
  });

  it('refreshes with the latest stored token when another tab rotated it', async () => {
    const store = TestBed.inject(AuthStore);
    await store.login({ email: 'a@b.c', password: 'secret-123' }); // in-memory + storage: refresh-1

    // Another tab rotated the token and persisted the new pair to localStorage.
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({
        token: 'tok-other',
        userId: 'u1',
        displayName: 'Ada',
        isAdmin: true,
        expiresAt: pastIso(), // access expired, so refresh proceeds
        refreshToken: 'refresh-9',
        refreshExpiresAt: futureIso(30 * 24 * 3_600_000),
      }),
    );
    api.refresh.mockReturnValue(of(authResponse({ refreshToken: 'refresh-10' })));

    await expect(store.refresh()).resolves.toBe(true);

    // The stale in-memory 'refresh-1' must NOT be spent (that would trip reuse-revocation).
    expect(api.refresh).toHaveBeenCalledWith('refresh-9');
  });

  it('adopts a session another tab wrote, and clears when another tab logs out', () => {
    const store = TestBed.inject(AuthStore);
    expect(store.session()).toBeNull();

    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({
        token: 't2',
        userId: 'u1',
        displayName: 'Ada',
        isAdmin: false,
        expiresAt: futureIso(),
        refreshToken: 'r2',
        refreshExpiresAt: futureIso(),
      }),
    );
    window.dispatchEvent(new StorageEvent('storage', { key: STORAGE_KEY }));
    expect(store.token()).toBe('t2');

    localStorage.removeItem(STORAGE_KEY);
    window.dispatchEvent(new StorageEvent('storage', { key: STORAGE_KEY }));
    expect(store.session()).toBeNull();
  });

  it('clears the session when the refresh token is rejected', async () => {
    const store = TestBed.inject(AuthStore);
    await store.login({ email: 'a@b.c', password: 'secret-123' });
    api.refresh.mockReturnValue(throwError(() => new Error('401')));

    await expect(store.refresh()).resolves.toBe(false);

    expect(store.session()).toBeNull();
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });

  it('resolves false without calling the API when there is nothing to refresh', async () => {
    const store = TestBed.inject(AuthStore);

    await expect(store.refresh()).resolves.toBe(false);

    expect(api.refresh).not.toHaveBeenCalled();
  });

  it('revokes the refresh token server-side and clears the session on logout', async () => {
    const store = TestBed.inject(AuthStore);
    await store.login({ email: 'a@b.c', password: 'secret-123' });

    store.logout();

    expect(api.logout).toHaveBeenCalledWith('refresh-1');
    expect(store.session()).toBeNull();
    expect(store.isAuthenticated()).toBe(false);
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });

  it('still clears the session when the server revocation fails', async () => {
    const store = TestBed.inject(AuthStore);
    await store.login({ email: 'a@b.c', password: 'secret-123' });
    api.logout.mockReturnValue(throwError(() => new Error('offline')));

    store.logout();

    expect(store.session()).toBeNull();
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });

  // The message now belongs to the store: the screen fires the command and never sees
  // the error. These two pin the wording, which nothing else covers.
  it('reports a rejected login without starting a session', () => {
    const store = TestBed.inject(AuthStore);
    api.login.mockReturnValue(throwError(() => ({ status: 401 })));

    store.login({ email: 'a@b.c', password: 'wrong' });

    expect(store.requestError()).toBe('Invalid email or password.');
    expect(store.pending()).toBe(false);
    expect(store.session()).toBeNull();
  });

  it('tells a user refused by policy to use the provider instead of retrying a password', () => {
    const store = TestBed.inject(AuthStore);
    // 403 = this instance no longer accepts app accounts at all. Reporting bad
    // credentials would send the user round in circles.
    api.login.mockReturnValue(throwError(() => ({ status: 403 })));

    store.login({ email: 'a@b.c', password: 'secret-123' });

    expect(store.requestError()).toContain('identity provider');
  });

  it('lands on the app after a successful sign-in', () => {
    const store = TestBed.inject(AuthStore);

    store.login({ email: 'a@b.c', password: 'secret-123' });

    expect(navigateByUrl).toHaveBeenCalledWith('/');
  });

  it('resumes the page the guard interrupted, rather than dropping the user on home', () => {
    routerUrl = '/login?returnUrl=%2Fcoffees%2F42';
    const store = TestBed.inject(AuthStore);

    store.login({ email: 'a@b.c', password: 'secret-123' });

    expect(navigateByUrl).toHaveBeenCalledWith('/coffees/42');
  });

  it.each(['//evil.example', 'https://evil.example/steal', 'javascript:alert(1)'])(
    'refuses to bounce a freshly signed-in user off-site via returnUrl=%s',
    (target) => {
      routerUrl = `/login?returnUrl=${encodeURIComponent(target)}`;
      const store = TestBed.inject(AuthStore);

      store.login({ email: 'a@b.c', password: 'secret-123' });

      expect(navigateByUrl).toHaveBeenCalledWith('/');
    },
  );

  it('toasts a rejected sign-in rather than leaving the screen to notice', () => {
    const store = TestBed.inject(AuthStore);
    api.login.mockReturnValue(throwError(() => ({ status: 401 })));

    store.login({ email: 'a@b.c', password: 'wrong' });

    expect(toast.show).toHaveBeenCalledWith('Invalid email or password.', 'error');
    expect(navigateByUrl).not.toHaveBeenCalled();
  });

  it('returns to the sign-in screen on logout', async () => {
    const store = TestBed.inject(AuthStore);
    await store.login({ email: 'a@b.c', password: 'secret-123' });
    navigateByUrl.mockClear();

    store.logout();

    expect(navigateByUrl).toHaveBeenCalledWith('/login');
  });

  it('clears a previous error so the sibling screen does not inherit it', () => {
    const store = TestBed.inject(AuthStore);
    api.login.mockReturnValue(throwError(() => ({ status: 401 })));
    store.login({ email: 'a@b.c', password: 'wrong' });

    store.resetRequestStatus();

    expect(store.requestError()).toBeNull();
  });
});
