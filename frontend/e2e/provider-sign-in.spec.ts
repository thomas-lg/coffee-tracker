import { test, expect, type Page } from '@playwright/test';
import { startFakeProvider, type FakeProvider } from './support/fake-oidc-provider';

/**
 * The provider sign-in round trip, driven through a real browser against a real
 * (if minimal) OpenID Connect provider.
 *
 * This exists because four bugs shipped through a green unit suite, none of them
 * reachable without a browser actually coming back from a redirect.
 *
 * Each guard below was verified by reintroducing the bug and watching the test go
 * red, a test that has never failed proves nothing:
 *
 *   caught  the callback racing the router (the exchange not awaited before bootstrap)
 *   caught  the ID token replayed on every reload
 *   NOT caught  the URL restored to /login behind a valid session. Reproducing it
 *               needs the library to restore the pre-authorize route, which it does
 *               against the real provider but not here; the mutation passes green.
 *               Left documented rather than faked into a test that would not mean it.
 *   out of scope  the provider's CORS configuration, that lives in the identity
 *                 provider, not in this repository.
 *
 * The API is stubbed rather than run: what broke was the client's handling of the
 * callback, and the server side is covered by the backend suite. The provider is
 * genuine, so discovery, PKCE, the nonce and the signature all really happen.
 */
test.describe('provider sign-in', () => {
  let provider: FakeProvider;
  /** Every POST to the sign-in endpoint, refused ones included, a replay has to be
   *  visible here, and counting only what succeeded would hide exactly that. */
  let attempts: string[];

  test.beforeEach(async ({ page }) => {
    attempts = [];
    provider = await startFakeProvider({
      sub: 'provider-subject-1',
      email: 'person@example.com',
      emailVerified: true,
      name: 'A Person',
      groups: ['home'],
    });

    await stubApi(page, provider, attempts);
  });

  test.afterEach(async () => {
    await provider.close();
  });

  test('lands on the app, not the login screen it started from', async ({ page }) => {
    await page.goto('/login');
    await page.getByRole('button', { name: /identity provider|Test Provider/i }).click();

    // The redirect comes back to '/', and the session has to be established before
    // any guard runs, otherwise the user is bounced to /login and the code is lost
    // with the URL.
    await expect(page).toHaveURL(/\/$/);
    await expect(page.locator('input[type="password"]')).toHaveCount(0);
    expect(attempts).toHaveLength(1);
  });

  test('survives a reload without replaying the token', async ({ page }) => {
    await page.goto('/login');
    await page.getByRole('button', { name: /identity provider|Test Provider/i }).click();
    await expect(page).toHaveURL(/\/$/);

    await page.reload();

    // Still signed in, and, the part that regressed, the token is not even posted a
    // second time. Asserting on attempts rather than successes is deliberate: a replay
    // is refused by the API, so counting successes would show 1 either way and the
    // regression would sail straight through.
    await expect(page.locator('input[type="password"]')).toHaveCount(0);
    expect(attempts).toHaveLength(1);
  });

  test('offers no provider action when none is configured', async ({ page }) => {
    await page.route('**/api/config', (route) =>
      route.fulfill({
        json: {
          localLoginEnabled: true,
          registrationEnabled: false,
          oidcAvailable: false,
          oidc: null,
        },
      }),
    );

    await page.goto('/login');

    await expect(page.locator('input[type="password"]')).toHaveCount(1);
    await expect(page.getByRole('button', { name: /identity provider/i })).toHaveCount(0);
  });

  test('shows why a refused sign-in failed instead of failing silently', async ({ page }) => {
    await page.route('**/api/auth/oidc', (route) =>
      route.fulfill({
        status: 409,
        json: { detail: 'An account already uses this email address.' },
      }),
    );

    await page.goto('/login');
    await page.getByRole('button', { name: /identity provider|Test Provider/i }).click();

    await expect(page.getByText(/already uses this email address/i)).toBeVisible();
    await expect(page.locator('input[type="password"]')).toHaveCount(1);
  });
});

/**
 * Stands in for the API: reports the fake provider through /api/config, and turns a
 * posted ID token into a session the way the real endpoint does, once per token.
 */
async function stubApi(page: Page, provider: FakeProvider, attempts: string[]): Promise<void> {
  await page.route('**/api/config', (route) =>
    route.fulfill({
      json: {
        localLoginEnabled: true,
        registrationEnabled: false,
        oidcAvailable: true,
        oidc: {
          authority: provider.issuer,
          clientId: provider.clientId,
          scopes: 'openid profile email groups',
          displayName: 'Test Provider',
        },
      },
    }),
  );

  const spent = new Set<string>();
  await page.route('**/api/auth/oidc', async (route) => {
    const idToken = (route.request().postDataJSON() as { idToken: string }).idToken;
    attempts.push(idToken);
    if (spent.has(idToken)) {
      // Mirrors the single-use registry: a replayed token is refused, which is what
      // turned a working session into a bounce to /login.
      return route.fulfill({ status: 401, json: { detail: 'Token already exchanged.' } });
    }
    spent.add(idToken);
    const in15Minutes = new Date(Date.now() + 15 * 60_000).toISOString();
    return route.fulfill({
      json: {
        token: 'e2e.app.token',
        expiresAt: in15Minutes,
        refreshToken: 'e2e-refresh',
        refreshExpiresAt: new Date(Date.now() + 14 * 86_400_000).toISOString(),
        userId: '11111111-1111-1111-1111-111111111111',
        displayName: 'A Person',
        isAdmin: false,
      },
    });
  });

  // Everything the app fetches once signed in; the catalogue itself is not what
  // these tests are about.
  await page.route('**/api/coffees**', (route) => route.fulfill({ json: [] }));
}
