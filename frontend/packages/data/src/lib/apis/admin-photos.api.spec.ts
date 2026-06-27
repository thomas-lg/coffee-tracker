import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminPhotosApi } from './admin-photos.api';

describe('AdminPhotosApi', () => {
  let api: AdminPhotosApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(AdminPhotosApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('GETs /api/admin/photos', () => {
    let result: unknown;
    api.list().subscribe((r) => (result = r));

    const req = http.expectOne('/api/admin/photos');
    expect(req.request.method).toBe('GET');
    req.flush([{ path: 'photos/a.jpg', used: false }]);

    expect(result).toEqual([{ path: 'photos/a.jpg', used: false }]);
  });

  it('DELETEs with the selected paths in the request body', () => {
    let result: { deleted: number; skipped: number } | undefined;
    api.delete(['photos/a.jpg', 'photos/b.jpg']).subscribe((r) => (result = r));

    const req = http.expectOne('/api/admin/photos');
    expect(req.request.method).toBe('DELETE');
    expect(req.request.body).toEqual({ paths: ['photos/a.jpg', 'photos/b.jpg'] });
    req.flush({ deleted: 2, skipped: 0 });

    expect(result).toEqual({ deleted: 2, skipped: 0 });
  });
});
