import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { ClientConfig } from '../models/models';

/** Public client config (anonymous) — e.g. whether registration is open. */
@Injectable({ providedIn: 'root' })
export class ConfigApi {
  private readonly http = inject(HttpClient);

  get(): Observable<ClientConfig> {
    return this.http.get<ClientConfig>('/api/config');
  }
}
