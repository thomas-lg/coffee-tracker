import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { ScanResult } from './models';

/** Snap-to-fill: OCR a bag photo into best-effort fields. May 503 when OCR is off. */
@Injectable({ providedIn: 'root' })
export class ScanApi {
  private readonly http = inject(HttpClient);

  scan(file: File): Observable<ScanResult> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ScanResult>('/api/coffees/scan', form);
  }
}
