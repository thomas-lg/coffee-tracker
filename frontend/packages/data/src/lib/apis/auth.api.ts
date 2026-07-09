import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { AuthResponse, Login, RefreshRequest, Register } from '../models/models';
import { SKIP_AUTH_REDIRECT } from '../http-context';

/** Anonymous auth endpoints. The session/token lives in @coffee-tracker/auth. */
@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);
  private readonly anon = { context: new HttpContext().set(SKIP_AUTH_REDIRECT, true) };

  login(dto: Login): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/auth/login', dto, this.anon);
  }

  register(dto: Register): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/auth/register', dto, this.anon);
  }

  /**
   * Exchanges a refresh token for a fresh, rotated access/refresh pair. Anonymous
   * (no Authorization header) — a 401 here means the refresh token is dead and must
   * reach the caller, not trigger the global redirect.
   */
  refresh(refreshToken: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(
      '/api/auth/refresh',
      { refreshToken } satisfies RefreshRequest,
      this.anon,
    );
  }

  /** Revokes the refresh token server-side (sign-out). Idempotent, returns 204. */
  logout(refreshToken: string): Observable<void> {
    return this.http.post<void>(
      '/api/auth/logout',
      { refreshToken } satisfies RefreshRequest,
      this.anon,
    );
  }
}
