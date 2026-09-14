import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { AccountSettings } from '../models/models';

/** The instance's account policy. Admin-only; the API enforces that. */
@Injectable({ providedIn: 'root' })
export class AdminSettingsApi {
  private readonly http = inject(HttpClient);

  get(): Observable<AccountSettings> {
    return this.http.get<AccountSettings>('/api/admin/settings');
  }

  /**
   * Saves the policy. Responds 409 when disabling local sign-in would leave no way
   * into the instance, the caller is expected to surface that, not swallow it.
   */
  update(settings: AccountSettings): Observable<AccountSettings> {
    return this.http.put<AccountSettings>('/api/admin/settings', settings);
  }
}
