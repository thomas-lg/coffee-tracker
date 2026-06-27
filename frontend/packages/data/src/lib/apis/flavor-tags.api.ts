import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { FlavorTag } from '../models/models';

/** The fixed, seeded set of flavor tags. */
@Injectable({ providedIn: 'root' })
export class FlavorTagsApi {
  private readonly http = inject(HttpClient);

  list(): Observable<FlavorTag[]> {
    return this.http.get<FlavorTag[]>('/api/flavor-tags');
  }
}
