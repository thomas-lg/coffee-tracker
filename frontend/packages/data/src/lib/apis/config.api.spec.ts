import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ConfigApi } from './config.api';

describe('ConfigApi', () => {
  let api: ConfigApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(ConfigApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('GETs /api/config and returns the flag', () => {
    let result: { registrationEnabled: boolean } | undefined;
    api.get().subscribe((r) => (result = r));

    const req = http.expectOne('/api/config');
    expect(req.request.method).toBe('GET');
    req.flush({ registrationEnabled: true });

    expect(result?.registrationEnabled).toBe(true);
  });
});
