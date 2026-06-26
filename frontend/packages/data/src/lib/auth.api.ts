import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { AuthResponse, Login, Register } from './models';

/** Anonymous auth endpoints. The session/token lives in @coffee-tracker/auth. */
@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);

  login(dto: Login): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/auth/login', dto);
  }

  register(dto: Register): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/auth/register', dto);
  }
}
