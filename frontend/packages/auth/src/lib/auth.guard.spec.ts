import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  provideRouter,
  UrlTree,
  type ActivatedRouteSnapshot,
  type RouterStateSnapshot,
} from '@angular/router';
import { AuthStore } from './auth.store';
import { authGuard } from './auth.guard';

/**
 * The guard decides who stays signed in. Until now it was reached only by two e2e tests,
 * both asserting "goes to /login", so the branch that actually keeps a returning user
 * in, `canRefresh() && await refresh()`, had no cover at all.
 */
describe('authGuard', () => {
  let hasValidAccessToken: ReturnType<typeof vi.fn>;
  let canRefresh: ReturnType<typeof vi.fn>;
  let refresh: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    hasValidAccessToken = vi.fn(() => false);
    canRefresh = vi.fn(() => false);
    refresh = vi.fn(() => Promise.resolve(false));

    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: AuthStore, useValue: { hasValidAccessToken, canRefresh, refresh } },
      ],
    });
  });

  afterEach(() => TestBed.resetTestingModule());

  const run = (url = '/coffees/42') =>
    TestBed.runInInjectionContext(() =>
      authGuard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot),
    ) as Promise<boolean | UrlTree>;

  it('lets a valid access token straight through', async () => {
    hasValidAccessToken.mockReturnValue(true);

    expect(await run()).toBe(true);
    // No point spending a refresh round trip on a session that is still good.
    expect(refresh).not.toHaveBeenCalled();
  });

  it('refreshes an expired access token instead of bouncing the user out', async () => {
    canRefresh.mockReturnValue(true);
    refresh.mockResolvedValue(true);

    expect(await run()).toBe(true);
    expect(refresh).toHaveBeenCalledOnce();
  });

  it('sends the user to /login when the refresh itself fails', async () => {
    canRefresh.mockReturnValue(true);
    refresh.mockResolvedValue(false);

    expect(await run()).toBeInstanceOf(UrlTree);
  });

  it('does not attempt a refresh it knows cannot work', async () => {
    canRefresh.mockReturnValue(false);

    expect(await run()).toBeInstanceOf(UrlTree);
    // A refresh token that is absent or expired would only buy a guaranteed 401.
    expect(refresh).not.toHaveBeenCalled();
  });

  it('carries the attempted page so sign-in can resume it', async () => {
    const result = (await run('/coffees/42')) as UrlTree;

    expect(result.toString()).toContain('/login');
    expect(result.queryParams['returnUrl']).toBe('/coffees/42');
  });

  it('carries the query string of the attempted page too', async () => {
    // A filtered shelf is a page worth returning to, not just its path.
    const result = (await run('/coffees?origin=Kenya')) as UrlTree;

    expect(result.queryParams['returnUrl']).toBe('/coffees?origin=Kenya');
  });
});
