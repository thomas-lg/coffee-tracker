import { Injectable, OnDestroy, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
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
  /** ISO date-time — access-token expiry. */
  expiresAt: string;
  /** Opaque rotated refresh token (absent only in pre-refresh stored sessions). */
  refreshToken?: string;
  /** ISO date-time — refresh-token expiry. */
  refreshExpiresAt?: string;
}

const STORAGE_KEY = 'ct.session';
/** Cross-tab lock name so only one tab refreshes at a time (Web Locks API). */
const REFRESH_LOCK = 'ct.auth.refresh';

/**
 * Signal-based auth store (native signals — `@ngrx/signals` has no Angular 22
 * release yet; this keeps the same public surface: state signals + computed +
 * methods).
 *
 * Expiry checks are plain methods, not `computed`s: a computed memoizes on its signal
 * dependencies, so a `Date.now()` comparison inside one would stay stale-truthy after
 * the token expires. Methods re-evaluate at guard/call time while still reading the
 * session signal (so templates stay reactive to login/logout).
 */
@Injectable({ providedIn: 'root' })
export class AuthStore implements OnDestroy {
  private readonly api = inject(AuthApi);
  private readonly _session = signal<Session | null>(restoreSession());
  /** Single-flight refresh: concurrent 401s share one /api/auth/refresh call. */
  private refreshInFlight: Promise<boolean> | null = null;

  readonly session = this._session.asReadonly();
  readonly token = computed(() => this._session()?.token ?? null);
  readonly displayName = computed(() => this._session()?.displayName ?? null);
  readonly isAdmin = computed(() => this._session()?.isAdmin ?? false);

  // Keep tabs in sync: another tab logging in/out or rotating the refresh token updates
  // localStorage, and this adopts it so we never present a stale (rotated) token — which
  // the server would treat as reuse and revoke the whole session.
  private readonly onStorage = (e: StorageEvent): void => {
    if (e.key === STORAGE_KEY || e.key === null) {
      this._session.set(readStoredSession());
    }
  };

  constructor() {
    if (typeof window !== 'undefined') {
      window.addEventListener('storage', this.onStorage);
    }
  }

  ngOnDestroy(): void {
    if (typeof window !== 'undefined') {
      window.removeEventListener('storage', this.onStorage);
    }
  }

  /** True while the access token itself is still valid. */
  hasValidAccessToken(): boolean {
    const s = this._session();
    return !!s && new Date(s.expiresAt).getTime() > Date.now();
  }

  /** True when a refresh token is stored and not yet expired. */
  canRefresh(): boolean {
    const s = this._session();
    return (
      !!s?.refreshToken &&
      !!s.refreshExpiresAt &&
      new Date(s.refreshExpiresAt).getTime() > Date.now()
    );
  }

  /**
   * Signed in = a live access token, or an expired one we can still refresh (the
   * interceptor/guard will transparently obtain a new pair).
   */
  isAuthenticated(): boolean {
    return this.hasValidAccessToken() || this.canRefresh();
  }

  async login(dto: Login): Promise<void> {
    this.persist(await firstValueFrom(this.api.login(dto)));
  }

  async register(dto: Register): Promise<void> {
    this.persist(await firstValueFrom(this.api.register(dto)));
  }

  /**
   * Exchanges the stored refresh token for a new access/refresh pair. Resolves true
   * on success; on failure (revoked/expired/reused token) the session is cleared.
   * Concurrent callers share a single in-flight request.
   */
  refresh(): Promise<boolean> {
    this.refreshInFlight ??= this.runRefresh().finally(() => (this.refreshInFlight = null));
    return this.refreshInFlight;
  }

  /**
   * Serialise the refresh across tabs (Web Locks): without this, two tabs holding the
   * same refresh token could both spend it, and the second would be flagged as token
   * reuse and revoke the whole family, logging everyone out. Falls back to a plain
   * refresh where the Locks API is unavailable (older browsers, tests).
   */
  private runRefresh(): Promise<boolean> {
    return typeof navigator !== 'undefined' && navigator.locks
      ? navigator.locks.request(REFRESH_LOCK, () => this.doRefresh())
      : this.doRefresh();
  }

  private async doRefresh(): Promise<boolean> {
    // Adopt the latest persisted session first: another tab may have rotated the refresh
    // token (possibly while we waited for the cross-tab lock), so use that token rather
    // than our stale one — presenting a rotated token is treated as reuse and would
    // revoke the whole session family.
    this._session.set(readStoredSession());

    const refreshToken = this._session()?.refreshToken;
    if (!refreshToken || !this.canRefresh()) return false;
    try {
      this.persist(await firstValueFrom(this.api.refresh(refreshToken)));
      return true;
    } catch {
      this.clearSession();
      return false;
    }
  }

  /** Revokes the refresh token server-side (fire-and-forget), then clears local state. */
  logout(): void {
    const refreshToken = this._session()?.refreshToken;
    if (refreshToken) {
      this.api.logout(refreshToken).subscribe({ error: () => {} });
    }
    this.clearSession();
  }

  /** Drops the local session only (no server call). */
  clearSession(): void {
    this._session.set(null);
    localStorage.removeItem(STORAGE_KEY);
  }

  private persist(res: AuthResponse): void {
    const session: Session = {
      token: res.token,
      userId: res.userId,
      displayName: res.displayName,
      isAdmin: res.isAdmin,
      expiresAt: res.expiresAt,
      refreshToken: res.refreshToken,
      refreshExpiresAt: res.refreshExpiresAt,
    };
    this._session.set(session);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
  }
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
