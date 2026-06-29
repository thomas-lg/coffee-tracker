import { test as base, Page, BrowserContext, expect } from '@playwright/test';

export interface SessionData {
  token: string;
  userId: string;
  displayName: string | null;
  isAdmin: boolean;
  expiresAt: string;
}

/**
 * Login via the UI and return the session data from localStorage.
 */
export async function loginViaUI(
  page: Page,
  email: string,
  password: string,
  expectedRedirect = '/'
): Promise<SessionData> {
  await page.goto('/login');
  await page.getByLabel(/email/i).fill(email);
  await page.getByLabel(/password/i).fill(password);
  await page.getByRole('button', { name: /sign in/i }).click();
  await page.waitForURL(expectedRedirect);

  // Extract session from localStorage
  const sessionJson = await page.evaluate(() => localStorage.getItem('ct.session'));
  if (!sessionJson) {
    throw new Error('Session not found in localStorage after login');
  }

  return JSON.parse(sessionJson) as SessionData;
}

/**
 * Logout via the UI and verify session is cleared.
 */
export async function logoutViaUI(page: Page): Promise<void> {
  await page.getByRole('button', { name: /sign out/i }).click();
  // After logout, should redirect to /login
  await page.waitForURL('/login');

  // Verify localStorage is cleared
  const sessionJson = await page.evaluate(() => localStorage.getItem('ct.session'));
  expect(sessionJson).toBeNull();
}

/**
 * Inject session data into localStorage before navigation.
 * Useful to skip login in tests that don't need to test login flow.
 */
export async function injectSession(page: Page, session: SessionData): Promise<void> {
  await page.addInitScript((sessionData) => {
    localStorage.setItem('ct.session', JSON.stringify(sessionData));
  }, session);
}

/**
 * Create an expired session (for testing token expiration scenarios).
 */
export function createExpiredSession(overrides: Partial<SessionData> = {}): SessionData {
  const now = new Date();
  const past = new Date(now.getTime() - 1000); // 1 second in the past
  return {
    token: 'expired-token-xyz',
    userId: '123',
    displayName: 'Test User',
    isAdmin: false,
    expiresAt: past.toISOString(),
    ...overrides,
  };
}

/**
 * Create a valid session (for testing authenticated routes).
 */
export function createValidSession(overrides: Partial<SessionData> = {}): SessionData {
  const now = new Date();
  const future = new Date(now.getTime() + 3600 * 1000); // 1 hour from now
  return {
    token: 'valid-token-xyz',
    userId: '123',
    displayName: 'Test User',
    isAdmin: false,
    expiresAt: future.toISOString(),
    ...overrides,
  };
}

/**
 * Extend Playwright's test with auth helpers.
 */
export const test = base.extend<{
  authenticatedPage: Page;
  adminPage: Page;
}>({
  /**
   * A page that's pre-authenticated with a valid session.
   */
  authenticatedPage: async ({ page }, use) => {
    const session = createValidSession();
    await injectSession(page, session);
    await page.goto('/');
    await use(page);
  },

  /**
   * A page that's pre-authenticated as an admin.
   */
  adminPage: async ({ page }, use) => {
    const session = createValidSession({ isAdmin: true });
    await injectSession(page, session);
    await page.goto('/');
    await use(page);
  },
});

export { expect };
