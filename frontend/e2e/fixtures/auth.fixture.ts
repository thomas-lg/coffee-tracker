import { test as base, type APIRequestContext, type Page } from '@playwright/test';

/**
 * Auth fixtures that mint their own users through the public API — exactly how
 * the backend integration suite does it. No backend seed, no hardcoded shared
 * credentials. Each test gets a freshly registered account so runs are isolated
 * and order-independent (the first-user-becomes-admin rule never matters here).
 */

/** localStorage key the AuthStore persists the session under (auth.store.ts). */
export const SESSION_KEY = 'ct.session';

/** Meets the API's 8+ char password rule. */
const PASSWORD = 'E2ePassw0rd!';

/** Shape returned by /api/auth/{register,login} and persisted by the AuthStore. */
export interface AuthResponse {
  token: string;
  expiresAt: string;
  userId: string;
  displayName: string | null;
  isAdmin: boolean;
}

interface StoredSession {
  token: string;
  userId: string;
  displayName: string | null;
  isAdmin: boolean;
  expiresAt: string;
}

export interface Account {
  email: string;
  password: string;
  auth: AuthResponse;
}

async function registerAccount(request: APIRequestContext, email: string): Promise<AuthResponse> {
  const res = await request.post('/api/auth/register', {
    data: { email, password: PASSWORD, displayName: 'E2E User' },
  });
  if (!res.ok()) {
    throw new Error(`register failed (${res.status()}): ${await res.text()}`);
  }
  return (await res.json()) as AuthResponse;
}

function sessionFrom(auth: AuthResponse): StoredSession {
  return {
    token: auth.token,
    userId: auth.userId,
    displayName: auth.displayName,
    isAdmin: auth.isAdmin,
    expiresAt: auth.expiresAt,
  };
}

/**
 * Seed a session into localStorage BEFORE the app boots, so AuthStore's
 * `restoreSession()` (which runs at construction) picks it up. addInitScript
 * re-runs on every navigation in the page, so the session survives reloads.
 */
export async function injectSession(page: Page, session: StoredSession): Promise<void> {
  await page.addInitScript(
    ([key, value]) => window.localStorage.setItem(key, value),
    [SESSION_KEY, JSON.stringify(session)] as const,
  );
}

export const test = base.extend<{
  /** A freshly registered account (created via the API, not the UI). */
  account: Account;
  /** A page that boots already authenticated as a fresh account. */
  authedPage: Page;
}>({
  account: async ({ request }, use, testInfo) => {
    const email = `e2e-${testInfo.workerIndex}-${testInfo.parallelIndex}-${Date.now()}@example.com`;
    const auth = await registerAccount(request, email);
    await use({ email, password: PASSWORD, auth });
  },
  authedPage: async ({ page, account }, use) => {
    await injectSession(page, sessionFrom(account.auth));
    await use(page);
  },
});

export { expect } from '@playwright/test';
