import { DestroyRef, computed, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  patchState,
  signalStore,
  withComputed,
  withHooks,
  withMethods,
  withProps,
  withState,
} from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { tapResponse } from '@ngrx/operators';
import { exhaustMap, firstValueFrom, fromEvent, pipe, tap } from 'rxjs';
import { setFulfilled, setPending, setRequestError, withRequestStatus } from '@coffee-tracker/util';
import { Router } from '@angular/router';
import { ToastService } from '@coffee-tracker/ui';
import { AuthApi, type AuthResponse, type Login, type Register } from '@coffee-tracker/data';

/**
 * The signed-in session. We trust the fields the API returns on login/register/refresh
 * (no JWT decoding) and persist them so the session survives reloads. The access token
 * is short-lived (~15 min); the refresh token keeps the session alive across expiries.
 */
interface Session {
  token: string;
  userId: string;
  displayName: string | null;
  isAdmin: boolean;
  /** ISO date-time, access-token expiry. */
  expiresAt: string;
  /** Opaque rotated refresh token (absent only in pre-refresh stored sessions). */
  refreshToken?: string;
  /** ISO date-time, refresh-token expiry. */
  refreshExpiresAt?: string;
}

const STORAGE_KEY = 'ct.session';
/** Cross-tab lock name so only one tab refreshes at a time (Web Locks API). */
const REFRESH_LOCK = 'ct.auth.refresh';

/**
 * Auth store.
 *
 * Expiry checks are plain methods, not `computed`s: a computed memoizes on its signal
 * dependencies, so a `Date.now()` comparison inside one would stay stale-truthy after
 * the token expires. Methods re-evaluate at guard/call time while still reading the
 * session signal (so templates stay reactive to login/logout).
 *
 * `refresh()` stays a Promise rather than becoming an `rxMethod`: the interceptor does
 * `from(auth.refresh()).pipe(switchMap(...))` and the guard awaits it, and both branch
 * on the resolved boolean.
 */
export const AuthStore = signalStore(
  { providedIn: 'root' },
  withState(() => ({ session: restoreSession() })),
  withRequestStatus(),
  withProps(() => ({
    _api: inject(AuthApi),
    _router: inject(Router),
    _toast: inject(ToastService),
    /**
     * Single-flight refresh: concurrent 401s share one /api/auth/refresh call. A box
     * rather than a bare field, because a store's props are readonly.
     */
    _refresh: { current: null as Promise<boolean> | null },
  })),
  withComputed(({ session }) => ({
    token: computed(() => session()?.token ?? null),
    displayName: computed(() => session()?.displayName ?? null),
    isAdmin: computed(() => session()?.isAdmin ?? false),
  })),
  withMethods((store) => {
    const hasValidAccessToken = (): boolean => {
      const s = store.session();
      return !!s && new Date(s.expiresAt).getTime() > Date.now();
    };

    const canRefresh = (): boolean => {
      const s = store.session();
      return (
        !!s?.refreshToken &&
        !!s.refreshExpiresAt &&
        new Date(s.refreshExpiresAt).getTime() > Date.now()
      );
    };

    /**
     * Where to land after a successful sign-in: back where the user was heading, or
     * home. Only same-origin relative paths are honoured, `//evil.com` and an absolute
     * URL are both things a crafted link could put in the query string to bounce a
     * freshly-authenticated user off-site.
     */
    const afterSignIn = (): string => {
      // Router's Params is an index signature of `any`; take it as unknown so the
      // narrowing below is what establishes the type.
      const target: unknown = store._router.parseUrl(store._router.url).queryParams['returnUrl'];
      return typeof target === 'string' && target.startsWith('/') && !target.startsWith('//')
        ? target
        : '/';
    };

    const clearSession = (): void => {
      patchState(store, { session: null });
      localStorage.removeItem(STORAGE_KEY);
    };

    const persist = (res: AuthResponse): void => {
      const session: Session = {
        token: res.token,
        userId: res.userId,
        displayName: res.displayName,
        isAdmin: res.isAdmin,
        expiresAt: res.expiresAt,
        refreshToken: res.refreshToken,
        refreshExpiresAt: res.refreshExpiresAt,
      };
      patchState(store, { session });
      localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    };

    const doRefresh = async (): Promise<boolean> => {
      // Adopt the latest persisted session first: another tab may have rotated the
      // refresh token (possibly while we waited for the cross-tab lock), so use that
      // token rather than our stale one, presenting a rotated token is treated as reuse
      // and would revoke the whole session family.
      patchState(store, { session: readStoredSession() });

      const refreshToken = store.session()?.refreshToken;
      if (!refreshToken || !canRefresh()) return false;
      try {
        persist(await firstValueFrom(store._api.refresh(refreshToken)));
        return true;
      } catch {
        clearSession();
        return false;
      }
    };

    /**
     * Serialise the refresh across tabs (Web Locks): without this, two tabs holding the
     * same refresh token could both spend it, and the second would be flagged as token
     * reuse and revoke the whole family, logging everyone out. Falls back to a plain
     * refresh where the Locks API is unavailable (older browsers, tests).
     */
    const runRefresh = (): Promise<boolean> =>
      typeof navigator !== 'undefined' && navigator.locks
        ? navigator.locks.request(REFRESH_LOCK, () => doRefresh())
        : doRefresh();

    return {
      hasValidAccessToken,
      canRefresh,
      clearSession,

      /**
       * Signed in = a live access token, or an expired one we can still refresh (the
       * interceptor/guard will transparently obtain a new pair).
       */
      isAuthenticated(): boolean {
        return hasValidAccessToken() || canRefresh();
      },

      /**
       * exhaustMap, not switchMap: a second sign-in must be ignored, not raced, and it
       * would spend one of the account's attempts before it is locked out. Enter in a
       * field submits without going through the inert button, so the operator is the
       * only thing that can refuse re-entry.
       */
      login: rxMethod<Login>(
        pipe(
          tap(() => patchState(store, setPending())),
          exhaustMap((dto) =>
            store._api.login(dto).pipe(
              tapResponse({
                next: (res) => {
                  persist(res);
                  patchState(store, setFulfilled());
                  void store._router.navigateByUrl(afterSignIn());
                },
                error: (err: unknown) => {
                  const message = loginMessage(err);
                  patchState(store, setRequestError(message));
                  store._toast.show(message, 'error');
                },
              }),
            ),
          ),
        ),
      ),

      /** Ignores re-entry for the same reason `login` does. */
      register: rxMethod<Register>(
        pipe(
          tap(() => patchState(store, setPending())),
          exhaustMap((dto) =>
            store._api.register(dto).pipe(
              tapResponse({
                next: (res) => {
                  persist(res);
                  patchState(store, setFulfilled());
                  void store._router.navigateByUrl(afterSignIn());
                },
                error: () => {
                  const message = 'Could not create the account. The email may already be in use.';
                  patchState(store, setRequestError(message));
                  store._toast.show(message, 'error');
                },
              }),
            ),
          ),
        ),
      ),

      /**
       * Exchanges a provider ID token for an app session. From here on the session is
       * indistinguishable from a local one, same token, same refresh, same guards.
       */
      async signInWithProviderToken(idToken: string): Promise<void> {
        persist(await firstValueFrom(store._api.oidcSignIn(idToken)));
      },

      /**
       * Exchanges the stored refresh token for a new access/refresh pair. Resolves true
       * on success; on failure (revoked/expired/reused token) the session is cleared.
       * Concurrent callers share a single in-flight request.
       */
      refresh(): Promise<boolean> {
        store._refresh.current ??= runRefresh().finally(() => (store._refresh.current = null));
        return store._refresh.current;
      },

      /**
       * Revokes the refresh token server-side (fire-and-forget), clears local state and
       * returns to the sign-in screen. Signing out is one action, so it lands in one
       * place, the header and the interceptor both just call this.
       */
      logout(): void {
        const refreshToken = store.session()?.refreshToken;
        if (refreshToken) {
          store._api.logout(refreshToken).subscribe({ error: () => {} });
        }
        clearSession();
        void store._router.navigateByUrl('/login');
      },
    };
  }),
  withHooks({
    onInit(store) {
      // Keep tabs in sync: another tab logging in/out or rotating the refresh token
      // updates localStorage, and this adopts it so we never present a stale (rotated)
      // token, which the server would treat as reuse and revoke the whole session.
      if (typeof window === 'undefined') return;
      fromEvent<StorageEvent>(window, 'storage')
        .pipe(takeUntilDestroyed(inject(DestroyRef)))
        .subscribe((e) => {
          if (e.key === STORAGE_KEY || e.key === null) {
            patchState(store, { session: readStoredSession() });
          }
        });
    },
  }),
);

export type AuthStore = InstanceType<typeof AuthStore>;

/**
 * 403 means the instance no longer accepts app accounts at all, telling the user their
 * password is wrong would send them round in circles. Lives here rather than in the
 * screen because the screen no longer sees the error.
 */
function loginMessage(err: unknown): string {
  return (err as { status?: number })?.status === 403
    ? 'This instance no longer accepts sign-in with an app account. Use the identity provider.'
    : 'Invalid email or password.';
}

/** Parse the stored session as-is (no usability filter); null if absent/corrupt. */
function readStoredSession(): Session | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as Session) : null;
  } catch {
    return null;
  }
}

/**
 * Restore a stored session, if it is still usable: a live access token, or an
 * expired one with a still-valid refresh token (refreshed on first use).
 */
function restoreSession(): Session | null {
  const session = readStoredSession();
  if (!session) return null;
  const accessAlive = new Date(session.expiresAt).getTime() > Date.now();
  const refreshAlive =
    !!session.refreshToken &&
    !!session.refreshExpiresAt &&
    new Date(session.refreshExpiresAt).getTime() > Date.now();
  if (!accessAlive && !refreshAlive) {
    localStorage.removeItem(STORAGE_KEY);
    return null;
  }
  return session;
}
