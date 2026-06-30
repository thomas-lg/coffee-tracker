import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { SKIP_AUTH_REDIRECT } from '@coffee-tracker/data';
import { authInterceptor } from './auth.interceptor';
import { AuthStore } from './auth.store';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpCtrl: HttpTestingController;
  let logout: ReturnType<typeof vi.fn>;
  let navigateByUrl: ReturnType<typeof vi.fn>;
  let token: string | null;

  beforeEach(() => {
    logout = vi.fn();
    navigateByUrl = vi.fn();
    token = 'jwt-abc';

    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        // Minimal stubs — the interceptor only reads token() and calls logout()/navigate.
        { provide: AuthStore, useValue: { token: () => token, logout } },
        { provide: Router, useValue: { navigateByUrl } },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpCtrl = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpCtrl.verify());

  it('attaches the bearer token to /api requests', () => {
    http.get('/api/coffees').subscribe();
    const req = httpCtrl.expectOne('/api/coffees');
    expect(req.request.headers.get('Authorization')).toBe('Bearer jwt-abc');
    req.flush([]);
  });

  it('does not attach the token to non-/api requests', () => {
    http.get('/assets/logo.svg').subscribe();
    const req = httpCtrl.expectOne('/assets/logo.svg');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush('');
  });

  it('attaches nothing when there is no token', () => {
    token = null;
    http.get('/api/coffees').subscribe();
    const req = httpCtrl.expectOne('/api/coffees');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush([]);
  });

  it('logs out and redirects to /login on a 401 from a normal request', () => {
    http.get('/api/coffees').subscribe({ next: () => {}, error: () => {} });
    httpCtrl.expectOne('/api/coffees').flush('nope', { status: 401, statusText: 'Unauthorized' });

    expect(logout).toHaveBeenCalledOnce();
    expect(navigateByUrl).toHaveBeenCalledWith('/login');
  });

  it('does not redirect on a 401 that opted out via SKIP_AUTH_REDIRECT', () => {
    const context = new HttpContext().set(SKIP_AUTH_REDIRECT, true);
    http.post('/api/auth/login', {}, { context }).subscribe({ next: () => {}, error: () => {} });
    httpCtrl.expectOne('/api/auth/login').flush('bad creds', { status: 401, statusText: 'Unauthorized' });

    expect(logout).not.toHaveBeenCalled();
    expect(navigateByUrl).not.toHaveBeenCalled();
  });

  it('passes non-401 errors through without logging out', () => {
    let errored = false;
    http.get('/api/coffees').subscribe({ next: () => {}, error: () => (errored = true) });
    httpCtrl.expectOne('/api/coffees').flush('boom', { status: 500, statusText: 'Server Error' });

    expect(errored).toBe(true);
    expect(logout).not.toHaveBeenCalled();
    expect(navigateByUrl).not.toHaveBeenCalled();
  });
});
