import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { ClientConfig } from '../models/models';
import { SKIP_AUTH_REDIRECT } from '../http-context';

/** Public client config (anonymous) — e.g. whether registration is open. */
@Injectable({ providedIn: 'root' })
export class ConfigApi {
  private readonly http = inject(HttpClient);

  get(): Observable<ClientConfig> {
    return this.http.get<ClientConfig>('/api/config', {
      context: new HttpContext().set(SKIP_AUTH_REDIRECT, true),
    });
  }
}
