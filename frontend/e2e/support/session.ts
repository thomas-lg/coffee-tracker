import { type APIRequestContext, type Page } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';

/** Helpers shared across e2e specs. The suite deliberately makes very few auth
 *  calls, the API rate-limits /api/auth to 10/min, so most setup is done by
 *  seeding a session directly rather than logging in repeatedly. */

/** localStorage key the AuthStore persists the session under (auth.store.ts). */
export const SESSION_KEY = 'ct.session';

/** Meets the API's 8+ char password rule. */
export const E2E_PASSWORD = 'E2ePassw0rd!';

/** A unique, valid email per call so registrations never collide across runs. */
export function uniqueEmail(prefix: string): string {
  return `e2e-${prefix}-${Date.now()}-${Math.floor(Math.random() * 1e6)}@example.com`;
}

/** The session object AuthStore stores/restores (auth.store.ts). */
export interface StoredSession {
  token: string;
  userId: string;
  displayName: string | null;
  isAdmin: boolean;
  /** ISO date-time, access-token expiry (short-lived, ~15 min). */
  expiresAt: string;
  /** Opaque refresh token; omit to simulate a session that cannot be refreshed. */
  refreshToken?: string;
  /** ISO date-time, refresh-token expiry. */
  refreshExpiresAt?: string;
}

/**
 * Seed a session into localStorage BEFORE the app boots, so AuthStore's
 * `restoreSession()` (which runs at construction) picks it up. addInitScript
 * re-runs on every full page load, so the session survives reloads.
 */
export async function injectSession(page: Page, session: StoredSession): Promise<void> {
  await page.addInitScript(([key, value]) => window.localStorage.setItem(key, value), [
    SESSION_KEY,
    JSON.stringify(session),
  ] as const);
}

/**
 * Where global setup leaves the suite's administrator, for tests that need one.
 * Resolved from this file rather than the process cwd, so the suite runs from anywhere:
 * `npx playwright test` in a repo root, or an IDE with its own cwd. __dirname rather than
 * import.meta: Playwright transpiles these to CommonJS (no "type": "module" here), so
 * import.meta.url is a syntax error at load time.
 */
export const ADMIN_STATE_FILE = resolve(__dirname, '../.auth/admin.json');

/** What the API hands back on register/login, trimmed to what the suite uses. */
export interface ProvisionedUser {
  token: string;
  userId: string;
  displayName: string | null;
  expiresAt: string;
  refreshToken: string;
  refreshExpiresAt: string;
}

/** The administrator claimed by global setup. */
export async function suiteAdmin(): Promise<ProvisionedUser & { email: string }> {
  return JSON.parse(await readFile(ADMIN_STATE_FILE, 'utf8')) as ProvisionedUser & {
    email: string;
  };
}

/**
 * Registers a fresh account through the API and returns its real tokens.
 *
 * Real rather than fabricated because the refresh flow has to be exercised against
 * a token the server actually issued, a made-up one proves nothing about it.
 */
export async function provisionUser(
  api: APIRequestContext,
  prefix: string,
): Promise<ProvisionedUser> {
  const res = await api.post('/api/auth/register', {
    data: { email: uniqueEmail(prefix), password: E2E_PASSWORD, displayName: `E2E ${prefix}` },
  });
  if (!res.ok()) {
    throw new Error(
      `could not provision an e2e user (${res.status()}); is registration still open?`,
    );
  }
  return (await res.json()) as ProvisionedUser;
}

/** Turns a provisioned user into the session shape AuthStore restores. */
export function sessionFor(
  user: ProvisionedUser,
  overrides: Partial<StoredSession> = {},
): StoredSession {
  return {
    token: user.token,
    userId: user.userId,
    displayName: user.displayName,
    isAdmin: false,
    expiresAt: user.expiresAt,
    refreshToken: user.refreshToken,
    refreshExpiresAt: user.refreshExpiresAt,
    ...overrides,
  };
}
