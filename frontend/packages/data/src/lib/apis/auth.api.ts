import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { AuthResponse, Login, Register } from '../models/models';
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
}
