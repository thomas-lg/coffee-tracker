import { test, expect } from '@playwright/test';
import { injectSession } from './support/session';

/**
 * Route-guard smoke — cheap (no auth calls): the guard must keep unauthenticated
 * and expired sessions out of the app and bounce them to /login.
 */
test.describe('route guards', () => {
  test('an unauthenticated visitor is sent to /login', async ({ page }) => {
    await page.goto('/coffees');

    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByRole('heading', { name: /sign in/i })).toBeVisible();
  });

  test('an expired session is sent to /login', async ({ page }) => {
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
