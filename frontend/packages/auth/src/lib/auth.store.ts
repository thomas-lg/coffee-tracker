import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AuthApi, type AuthResponse, type Login, type Register } from '@coffee-tracker/data';

/**
 * The signed-in session. We trust the fields the API returns on login/register
 * (no JWT decoding) and persist them so the session survives reloads until expiry.
 */
interface Session {
  token: string;
  userId: string;
  displayName: string | null;
  isAdmin: boolean;
  /** ISO date-time */
  expiresAt: string;
}

const STORAGE_KEY = 'ct.session';

/**
 * Signal-based auth store (native signals — `@ngrx/signals` has no Angular 22
 * release yet; this keeps the same public surface: state signals + computed +
 * methods).
 */
@Injectable({ providedIn: 'root' })
export class AuthStore {
  private readonly api = inject(AuthApi);
  private readonly _session = signal<Session | null>(restoreSession());

  readonly session = this._session.asReadonly();
  readonly token = computed(() => this._session()?.token ?? null);
  readonly displayName = computed(() => this._session()?.displayName ?? null);
  readonly isAdmin = computed(() => this._session()?.isAdmin ?? false);
  readonly isAuthenticated = computed(() => {
    const s = this._session();
    return !!s && new Date(s.expiresAt).getTime() > Date.now();
  });

  async login(dto: Login): Promise<void> {
    this.persist(await firstValueFrom(this.api.login(dto)));
  }

  async register(dto: Register): Promise<void> {
    this.persist(await firstValueFrom(this.api.register(dto)));
  }

  logout(): void {
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
    };
    this._session.set(session);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
  }
}

/** Restore a non-expired session from localStorage, if any. */
function restoreSession(): Session | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const session = JSON.parse(raw) as Session;
    if (new Date(session.expiresAt).getTime() <= Date.now()) {
      localStorage.removeItem(STORAGE_KEY);
      return null;
    }
    return session;
  } catch {
    return null;
  }
}
