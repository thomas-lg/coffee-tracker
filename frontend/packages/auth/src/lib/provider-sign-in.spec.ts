import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { AuthStore } from './auth.store';
import { ProviderSignIn } from './provider-sign-in';

/**
 * The callback handler had no unit cover, only the Playwright suite — which documents
 * one bug it cannot catch: the URL being restored to /login behind a perfectly valid
 * session. That one is asserted here, because the fix is a single history.replaceState
 * call and nothing else in the flow reveals it.
 */
describe('ProviderSignIn.complete', () => {
  let checkAuth: ReturnType<typeof vi.fn>;
  let logoffLocal: ReturnType<typeof vi.fn>;
  let signInWithProviderToken: ReturnType<typeof vi.fn>;
  let replaceState: ReturnType<typeof vi.spyOn>;

  /** Puts the browser where the provider would have left it. */
  const arriveAt = (search: string) => history.replaceState(null, '', `/login${search}`);

  beforeEach(() => {
    checkAuth = vi.fn(() => of({ isAuthenticated: true, idToken: 'id-token-1' }));
    logoffLocal = vi.fn();
    signInWithProviderToken = vi.fn(() => Promise.resolve());
    replaceState = vi.spyOn(history, 'replaceState');

    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        { provide: OidcSecurityService, useValue: { checkAuth, logoffLocal, authorize: vi.fn() } },
        { provide: AuthStore, useValue: { signInWithProviderToken } },
      ],
    });
  });

  afterEach(() => {
    replaceState.mockRestore();
    history.replaceState(null, '', '/');
    TestBed.resetTestingModule();
  });

  const complete = () => TestBed.inject(ProviderSignIn).complete();

  it('does nothing on an ordinary page load', async () => {
    arriveAt('');

    expect(await complete()).toBe(false);
    // checkAuth() keeps its own session and would hand back the same ID token on every
    // later load — posting that again asks the API to spend a token it already spent.
    expect(checkAuth).not.toHaveBeenCalled();
  });

  it('establishes a session from a provider callback', async () => {
    arriveAt('?code=abc&state=xyz');
    const service = TestBed.inject(ProviderSignIn);

    expect(await service.complete()).toBe(true);
    expect(signInWithProviderToken).toHaveBeenCalledWith('id-token-1');
    expect(service.justSignedIn()).toBe(true);
    expect(service.error()).toBeNull();
  });

  // The bug provider-sign-in.spec.ts documents as not catchable end-to-end: checkAuth()
  // restores the route the user left from, which is /login. Reading the pathname back
  // would park the router on the login screen with a valid session behind it.
  it('sends the router to the app root, not back to the login screen it came from', async () => {
    arriveAt('?code=abc');

    await complete();

    expect(replaceState).toHaveBeenCalledWith(null, '', '/');
  });

  it('drops the authorization code from the URL', async () => {
    arriveAt('?code=abc&state=xyz');

    await complete();

    // A reload carrying a spent code would read as a failed sign-in.
    expect(window.location.search).toBe('');
  });

  it('reports a provider refusal rather than failing silently', async () => {
    arriveAt('?error=access_denied');
    const service = TestBed.inject(ProviderSignIn);

    expect(await service.complete()).toBe(false);
    expect(service.error()).toMatch(/did not authorise you/i);
    // The library's session goes too, or the next load retries a sign-in that just failed.
    expect(logoffLocal).toHaveBeenCalled();
    expect(checkAuth).not.toHaveBeenCalled();
  });

  it("prefers the provider's own wording when it sends one", async () => {
    arriveAt('?error=access_denied&error_description=Your%20account%20is%20not%20in%20the%20group');
    const service = TestBed.inject(ProviderSignIn);

    await service.complete();

    expect(service.error()).toBe('Your account is not in the group');
  });

  it('falls back to a generic message for a refusal it does not recognise', async () => {
    arriveAt('?error=temporarily_unavailable');
    const service = TestBed.inject(ProviderSignIn);

    await service.complete();

    expect(service.error()).toMatch(/refused the sign-in/i);
  });

  it('lets the app start when the provider is unreachable', async () => {
    arriveAt('?code=abc');
    checkAuth.mockReturnValue(throwError(() => new Error('network down')));
    const service = TestBed.inject(ProviderSignIn);

    // This runs in an app initializer: a throw here would stop the app booting, and
    // local sign-in still works.
    expect(await service.complete()).toBe(false);
    expect(service.justSignedIn()).toBe(false);
  });

  it('gives up quietly when the library reports no session', async () => {
    arriveAt('?code=abc');
    checkAuth.mockReturnValue(of({ isAuthenticated: false, idToken: null }));

    expect(await complete()).toBe(false);
    expect(signInWithProviderToken).not.toHaveBeenCalled();
  });

  it("surfaces the API's explanation when it refuses the token", async () => {
    arriveAt('?code=abc');
    // The conflicting-unverified-email case: the API's detail is the useful one.
    signInWithProviderToken.mockRejectedValue({
      error: { detail: 'An app account already uses that address.' },
    });
    const service = TestBed.inject(ProviderSignIn);

    expect(await service.complete()).toBe(false);
    expect(service.error()).toBe('An app account already uses that address.');
    expect(service.justSignedIn()).toBe(false);
    expect(logoffLocal).toHaveBeenCalled();
  });

  it('still says something when the refusal carries no detail', async () => {
    arriveAt('?code=abc');
    signInWithProviderToken.mockRejectedValue(new Error('boom'));
    const service = TestBed.inject(ProviderSignIn);

    await service.complete();

    expect(service.error()).toMatch(/failed/i);
  });
});
