import { type Page } from '@playwright/test';

/** Helpers shared across e2e specs. The suite deliberately makes very few auth
 *  calls — the API rate-limits /api/auth to 10/min — so most setup is done by
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
  /** ISO date-time — access-token expiry (short-lived, ~15 min). */
  expiresAt: string;
  /** Opaque refresh token; omit to simulate a session that cannot be refreshed. */
  refreshToken?: string;
  /** ISO date-time — refresh-token expiry. */
  refreshExpiresAt?: string;
}

/**
 * Seed a session into localStorage BEFORE the app boots, so AuthStore's
 * `restoreSession()` (which runs at construction) picks it up. addInitScript
 * re-runs on every full page load, so the session survives reloads.
 */
export async function injectSession(page: Page, session: StoredSession): Promise<void> {
  await page.addInitScript(
    ([key, value]) => window.localStorage.setItem(key, value),
    [SESSION_KEY, JSON.stringify(session)] as const,
  );
}
