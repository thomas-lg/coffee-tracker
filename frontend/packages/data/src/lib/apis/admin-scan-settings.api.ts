import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { OcrEngine, ScanSettings } from '../models/models';

/** Reads and changes the OCR engine the instance scans with. Administrators only. */
@Injectable({ providedIn: 'root' })
export class AdminScanSettingsApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/admin/scan-settings';

  get(): Observable<ScanSettings> {
    return this.http.get<ScanSettings>(this.base);
  }

  setEngine(engine: OcrEngine): Observable<ScanSettings> {
    return this.http.put<ScanSettings>(this.base, { engine });
  }
}
