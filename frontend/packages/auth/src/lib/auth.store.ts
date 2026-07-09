import { Injectable, computed, inject, signal } from '@angular/core';
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
export class AuthStore {
  private readonly api = inject(AuthApi);
  private readonly _session = signal<Session | null>(restoreSession());
  /** Single-flight refresh: concurrent 401s share one /api/auth/refresh call. */
  private refreshInFlight: Promise<boolean> | null = null;

  readonly session = this._session.asReadonly();
  readonly token = computed(() => this._session()?.token ?? null);
  readonly displayName = computed(() => this._session()?.displayName ?? null);
  readonly isAdmin = computed(() => this._session()?.isAdmin ?? false);

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
    this.refreshInFlight ??= this.doRefresh().finally(() => (this.refreshInFlight = null));
    return this.refreshInFlight;
  }

  private async doRefresh(): Promise<boolean> {
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

/**
 * Restore a stored session, if it is still usable: a live access token, or an
 * expired one with a still-valid refresh token (refreshed on first use).
 */
function restoreSession(): Session | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const session = JSON.parse(raw) as Session;
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
  } catch {
    return null;
  }
}
