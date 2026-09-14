import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { Backup, ImportResult } from '../models/models';

/** Admin-only export and restore of the whole catalog. */
@Injectable({ providedIn: 'root' })
export class AdminBackupApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/admin/backup';

  export(): Observable<Backup> {
    return this.http.get<Backup>(this.base);
  }

  /** Replaces the catalog. Destructive, the screen confirms before calling this. */
  import(backup: Backup): Observable<ImportResult> {
    return this.http.post<ImportResult>(this.base, backup);
  }
}
