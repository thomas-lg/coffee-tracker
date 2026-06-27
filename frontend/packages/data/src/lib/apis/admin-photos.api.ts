import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { PhotoListItem, PhotoDeleteResult } from '../models/models';

/** Admin-only photo housekeeping: list stored photos and delete selected orphans. */
@Injectable({ providedIn: 'root' })
export class AdminPhotosApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/admin/photos';

  list(): Observable<PhotoListItem[]> {
    return this.http.get<PhotoListItem[]>(this.base);
  }

  /** DELETE carries the selected paths in the body (HttpClient supports `body` here). */
  delete(paths: string[]): Observable<PhotoDeleteResult> {
    return this.http.delete<PhotoDeleteResult>(this.base, { body: { paths } });
  }
}
