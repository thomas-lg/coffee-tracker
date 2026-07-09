import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { SKIP_AUTH_REDIRECT } from '@coffee-tracker/data';
import { RELOAD } from '@coffee-tracker/util';
import { authInterceptor } from './auth.interceptor';
import { AuthStore } from './auth.store';

/** The header shape ASP.NET Core's JwtBearer challenge puts on its 401s. */
const API_CHALLENGE = { 'WWW-Authenticate': 'Bearer error="invalid_token"' };

/** Lets the interceptor's `from(auth.refresh())` microtask settle. */
const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpCtrl: HttpTestingController;
  let logout: ReturnType<typeof vi.fn>;
  let refresh: ReturnType<typeof vi.fn>;
  let navigateByUrl: ReturnType<typeof vi.fn>;
  let reload: ReturnType<typeof vi.fn>;
  let token: string | null;
  let canRefresh: boolean;

  beforeEach(() => {
    logout = vi.fn();
    navigateByUrl = vi.fn();
    reload = vi.fn();
    token = 'jwt-abc';
    canRefresh = false;
    // Successful refresh rotates the token the interceptor re-reads for the retry.
    refresh = vi.fn(async () => {
      token = 'jwt-new';
      return true;
    });
    sessionStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        // Minimal stub — the interceptor reads token()/canRefresh() and calls
        // refresh()/logout()/navigate.
        {
          provide: AuthStore,
          useValue: {
            token: () => token,
            canRefresh: () => canRefresh,
            refresh,
            logout,
          },
        },
        { provide: Router, useValue: { navigateByUrl } },
        { provide: RELOAD, useValue: reload },
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

  it('logs out and redirects to /login on an API 401 when no refresh token is available', () => {
    http.get('/api/coffees').subscribe({ next: () => {}, error: () => {} });
    httpCtrl
      .expectOne('/api/coffees')
      .flush('nope', { status: 401, statusText: 'Unauthorized', headers: API_CHALLENGE });

    expect(refresh).not.toHaveBeenCalled();
    expect(logout).toHaveBeenCalledOnce();
    expect(navigateByUrl).toHaveBeenCalledWith('/login');
    expect(reload).not.toHaveBeenCalled();
  });

  it('refreshes and retries the request once on an API 401 when a refresh token exists', async () => {
    canRefresh = true;
    let result: unknown;
    http.get('/api/coffees').subscribe({ next: (r) => (result = r), error: () => {} });
    httpCtrl
      .expectOne('/api/coffees')
      .flush('expired', { status: 401, statusText: 'Unauthorized', headers: API_CHALLENGE });

    await settle();
    const retried = httpCtrl.expectOne('/api/coffees');
    expect(retried.request.headers.get('Authorization')).toBe('Bearer jwt-new');
    retried.flush(['ok']);

    expect(result).toEqual(['ok']);
    expect(refresh).toHaveBeenCalledOnce();
    expect(logout).not.toHaveBeenCalled();
    expect(navigateByUrl).not.toHaveBeenCalled();
  });

  it('redirects to /login (without retrying) when the refresh itself fails', async () => {
    canRefresh = true;
    refresh.mockResolvedValue(false); // refresh() clears the session itself on failure
    let errored = false;
    http.get('/api/coffees').subscribe({ next: () => {}, error: () => (errored = true) });
    httpCtrl
      .expectOne('/api/coffees')
      .flush('expired', { status: 401, statusText: 'Unauthorized', headers: API_CHALLENGE });

    await settle();

    expect(errored).toBe(true);
    expect(navigateByUrl).toHaveBeenCalledWith('/login');
    httpCtrl.expectNone('/api/coffees');
  });

  it('gives up (logout + /login) when the retried request still 401s — no refresh loop', async () => {
    canRefresh = true;
    let errored = false;
    http.get('/api/coffees').subscribe({ next: () => {}, error: () => (errored = true) });
    httpCtrl
      .expectOne('/api/coffees')
      .flush('expired', { status: 401, statusText: 'Unauthorized', headers: API_CHALLENGE });

    await settle();
    httpCtrl
      .expectOne('/api/coffees')
      .flush('still nope', { status: 401, statusText: 'Unauthorized', headers: API_CHALLENGE });

    expect(errored).toBe(true);
    expect(refresh).toHaveBeenCalledOnce();
    expect(logout).toHaveBeenCalledOnce();
    expect(navigateByUrl).toHaveBeenCalledWith('/login');
    httpCtrl.expectNone('/api/coffees');
  });

  it('reloads the page (instead of logging out) on a gateway 401 without the API challenge header', () => {
    http.get('/api/coffees').subscribe({ next: () => {}, error: () => {} });
    httpCtrl.expectOne('/api/coffees').flush('nope', { status: 401, statusText: 'Unauthorized' });

    expect(reload).toHaveBeenCalledOnce();
    expect(logout).not.toHaveBeenCalled();
    expect(refresh).not.toHaveBeenCalled();
    expect(navigateByUrl).not.toHaveBeenCalled();
  });

  it('throttles gateway reloads and lets the error propagate instead of looping', () => {
    let errors = 0;
    const fail = { next: () => {}, error: () => errors++ };

    http.get('/api/coffees').subscribe(fail);
    httpCtrl.expectOne('/api/coffees').flush('nope', { status: 401, statusText: 'Unauthorized' });
    http.get('/api/coffees').subscribe(fail);
    httpCtrl.expectOne('/api/coffees').flush('nope', { status: 401, statusText: 'Unauthorized' });

    expect(reload).toHaveBeenCalledOnce();
    expect(errors).toBe(2);
    expect(logout).not.toHaveBeenCalled();
  });

  it('does not redirect, refresh, or reload on a 401 that opted out via SKIP_AUTH_REDIRECT', () => {
    const context = new HttpContext().set(SKIP_AUTH_REDIRECT, true);
    http.post('/api/auth/refresh', {}, { context }).subscribe({ next: () => {}, error: () => {} });
    httpCtrl.expectOne('/api/auth/refresh').flush('dead token', { status: 401, statusText: 'Unauthorized' });

    expect(logout).not.toHaveBeenCalled();
    expect(refresh).not.toHaveBeenCalled();
    expect(navigateByUrl).not.toHaveBeenCalled();
    expect(reload).not.toHaveBeenCalled();
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
