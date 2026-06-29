import { test, expect, injectSession, SESSION_KEY } from './fixtures/auth.fixture';

/**
 * Minimal, fully-green auth smoke suite. Covers the guard boundary (in both
 * directions) and a real end-to-end login through the form. Selectors are
 * semantic (getByLabel/getByRole) — the app has no data-testid attributes.
 */

test.describe('authentication', () => {
  test('unauthenticated visitor is redirected from a guarded route to /login', async ({ page }) => {
    await page.goto('/coffees');

    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByRole('heading', { name: /sign in/i })).toBeVisible();
  });

  test('a registered user can sign in through the form', async ({ page, account }) => {
    await page.goto('/login');

    await page.getByLabel(/email/i).fill(account.email);
    await page.getByLabel(/password/i).fill(account.password);
    await page.getByRole('button', { name: /sign in/i }).click();

    // onSubmit navigates to '/' on success; landing on the origin root (not /login
    // or any feature route) proves the guard let us in.
    await expect(page).toHaveURL(/^https?:\/\/[^/]+\/$/);

    const stored = await page.evaluate((k) => window.localStorage.getItem(k), SESSION_KEY);
    expect(stored).toBeTruthy();
    expect(JSON.parse(stored as string).token).toBeTruthy();
  });

  test('an authenticated session can reach the guarded catalog', async ({ authedPage }) => {
    await authedPage.goto('/coffees');

    // Stayed on /coffees (no bounce to /login) and the login form is absent.
    await expect(authedPage).toHaveURL(/\/coffees$/);
    await expect(authedPage.getByRole('heading', { name: /sign in/i })).toHaveCount(0);
  });

  test('an expired session is redirected to /login', async ({ page }) => {
    await injectSession(page, {
      token: 'expired.placeholder.token',
      userId: '00000000-0000-0000-0000-000000000000',
      displayName: null,
      isAdmin: false,
      expiresAt: new Date(Date.now() - 60_000).toISOString(),
    });

    await page.goto('/coffees');

    await expect(page).toHaveURL(/\/login$/);
  });
});
