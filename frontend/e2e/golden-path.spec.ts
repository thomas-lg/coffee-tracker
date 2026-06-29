import { test, expect } from '@playwright/test';
import { uniqueEmail, E2E_PASSWORD, SESSION_KEY } from './support/session';

/**
 * The golden path — the journey a real user takes on their first visit:
 * create an account, add a coffee, see it on the shelf, then rate it. One
 * end-to-end flow through the actual UI (no API shortcuts, no data-testid —
 * semantic locators only). Makes a single auth call (one register) to stay
 * clear of the /api/auth 10/min rate limit.
 */
test('golden path: register, add a coffee, see it on the shelf, and rate it', async ({ page }) => {
  const email = uniqueEmail('golden');
  const coffeeName = `E2E Yirgacheffe ${Date.now()}`;

  // 1 — Register through the form; on success the app lands on home, signed in.
  await page.goto('/register');
  await page.getByLabel(/display name/i).fill('Golden Path');
  await page.getByLabel(/email/i).fill(email);
  await page.getByLabel(/password/i).fill(E2E_PASSWORD);
  await page.getByRole('button', { name: /create account/i }).click();

  await expect(page).toHaveURL(/^https?:\/\/[^/]+\/$/);
  const stored = await page.evaluate((k) => window.localStorage.getItem(k), SESSION_KEY);
  expect(JSON.parse(stored as string).token).toBeTruthy();

  // 2 — Add a coffee from the catalog's "Add a coffee" action.
  await page.goto('/coffees');
  await page.getByRole('link', { name: /add a coffee/i }).click();
  await expect(page).toHaveURL(/\/coffees\/new$/);

  await page.getByLabel('Name').fill(coffeeName);
  await page.getByLabel('Roaster').fill('E2E Roastery');
  await page.getByLabel('Origin').fill('Ethiopia');
  await page.getByLabel('Roast level').selectOption({ label: 'Medium' });
  await page.getByLabel(/date bought/i).fill('2026-01-15');
  await page.getByRole('button', { name: /add coffee/i }).click();

  // 3 — Create lands on the new coffee's detail page, headed by its name.
  await expect(page).toHaveURL(/\/coffees\/\d+$/);
  await expect(page.getByRole('heading', { name: coffeeName })).toBeVisible();

  // 4 — Rate it today: pick 4 stars and save; the count flips to "1 rating".
  await page.getByRole('group', { name: /rate this coffee/i }).getByRole('button', { name: '4 stars' }).click();
  await page.getByRole('button', { name: /save today.s rating/i }).click();
  await expect(page.getByText(/^1 rating$/)).toBeVisible();

  // 5 — Back on the shelf, the coffee is listed.
  await page.goto('/coffees');
  await expect(page.getByText(coffeeName)).toBeVisible();
});
