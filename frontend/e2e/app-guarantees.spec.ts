import { test, expect } from '@playwright/test';
import { injectSession, provisionUser, sessionFor, suiteAdmin } from './support/session';

/**
 * The promises the app makes beyond the happy path: that one user cannot edit
 * another's coffee, that a session outlives its short access token, that the admin
 * screens are shut to non-admins, that the lock-out guard actually refuses, and that
 * the sign-in screen offers only what the instance accepts.
 *
 * The first four run against the real API — the account policy and its guard went
 * into production the day they were written, which is the worst moment to be leaning
 * on unit tests alone. The last two stub /api/config on purpose: the setting is
 * instance-wide, and flipping it mid-run would break whichever other spec happened to
 * be registering at the time.
 */

test.describe('ownership', () => {
  test('a coffee can only be edited by the person who added it', async ({ page, request }) => {
    const owner = await provisionUser(request, 'owner');
    const stranger = await provisionUser(request, 'stranger');

    const created = await request.post('/api/coffees', {
      headers: { authorization: `Bearer ${owner.token}` },
      data: {
        name: `Owned ${Date.now()}`,
        roaster: 'E2E Roastery',
        origin: 'Ethiopia',
        roastLevel: 'Medium',
        price: 12.5,
        dateBought: '2026-01-15',
        shopName: null,
        purchaseUrl: null,
      },
    });
    expect(created.ok()).toBeTruthy();
    const coffee = (await created.json()) as { id: number };

    // The API is the authority here — the UI hiding a button would not be a guarantee.
    const rejected = await request.put(`/api/coffees/${coffee.id}`, {
      headers: { authorization: `Bearer ${stranger.token}` },
      data: {
        name: 'Hijacked',
        roaster: 'E2E Roastery',
        origin: 'Ethiopia',
        roastLevel: 'Medium',
        price: 12.5,
        dateBought: '2026-01-15',
        shopName: null,
        purchaseUrl: null,
      },
    });
    expect(rejected.status()).toBe(403);

    // And the stranger is not offered the edit either.
    await injectSession(page, sessionFor(stranger));
    await page.goto(`/coffees/${coffee.id}`);
    await expect(page.getByRole('link', { name: /^edit$/i })).toHaveCount(0);
  });
});

test.describe('session lifetime', () => {
  test('an expired access token is refreshed instead of bouncing the user out', async ({ page, request }) => {
    const user = await provisionUser(request, 'refresh');

    // A real refresh token, and an access token dated into the past: exactly the
    // state a user comes back to after leaving a tab open. Fabricating the refresh
    // token would prove nothing — the server has to recognise it.
    await injectSession(
      page,
      sessionFor(user, { expiresAt: new Date(Date.now() - 60_000).toISOString() }),
    );

    await page.goto('/coffees');

    await expect(page).toHaveURL(/\/coffees$/);
    await expect(page.getByRole('heading', { name: /browse your shelf/i })).toBeVisible();

    // The guard swapped the dead token for a live one rather than sending them to /login.
    const stored = await page.evaluate(
      () => JSON.parse(localStorage.getItem('ct.session') ?? '{}') as { expiresAt?: string },
    );
    expect(new Date(stored.expiresAt ?? 0).getTime()).toBeGreaterThan(Date.now());
  });
});

test.describe('admin screens', () => {
  test('a non-admin is turned away and offered no way in', async ({ page, request }) => {
    const user = await provisionUser(request, 'plain');
    await injectSession(page, sessionFor(user, { isAdmin: false }));

    // Both sections, since the guard now sits on the shell rather than on each one.
    await page.goto('/admin/settings');
    await expect(page).not.toHaveURL(/\/admin/);
    await page.goto('/admin/photos');
    await expect(page).not.toHaveURL(/\/admin/);

    await page.goto('/');
    await expect(page.getByRole('link', { name: /^admin$/i })).toHaveCount(0);
  });

  test('the admin sections are reachable as tabs', async ({ page }) => {
    const admin = await suiteAdmin();
    await injectSession(page, sessionFor(admin, { isAdmin: true }));

    // One nav entry now; the sections are tabs inside it.
    await page.goto('/');
    await expect(page.getByRole('link', { name: /^accounts$/i })).toHaveCount(0);
    await page.getByRole('link', { name: /^admin$/i }).click();

    // Bare /admin lands on Photos.
    await expect(page).toHaveURL(/\/admin\/photos/);
    await expect(page.getByRole('heading', { name: /photo cleanup/i })).toBeVisible();

    await page.getByRole('link', { name: /^accounts$/i }).click();
    await expect(page).toHaveURL(/\/admin\/settings/);
    await expect(page.getByRole('heading', { name: /account settings/i })).toBeVisible();

    await page.getByRole('link', { name: /^backup$/i }).click();
    await expect(page).toHaveURL(/\/admin\/backup/);
    await expect(page.getByRole('heading', { name: /^backup$/i })).toBeVisible();
  });

  test('disabling local sign-in is refused while it is the only way in', async ({ page }) => {
    const admin = await suiteAdmin();
    await injectSession(page, sessionFor(admin, { isAdmin: true }));

    await page.goto('/admin/settings');
    // By name, not position: both switches are checkboxes, and swapping them in the
    // template would leave .first() silently asserting against the wrong one.
    const signIn = page.getByRole('checkbox', { name: /sign in with an app account/i });
    await expect(signIn).toBeChecked();

    await signIn.uncheck();

    // No identity provider is configured in the suite, so turning this off would
    // leave nobody able to get in. The refusal has to be explained on screen — an
    // administrator who cannot see why a switch refused to move reaches for SQL.
    //
    // Matched on the API's own wording, not just "identity provider": that phrase
    // also appears in the setting's description, so a looser locator would pass even
    // if the refusal were never shown.
    await expect(page.getByText(/at least once with an administrator account/i)).toBeVisible();
    await expect(signIn).toBeChecked();
  });
});

test.describe('local accounts', () => {
  test('the client stops offering registration once it is closed', async ({ page }) => {
    // Stubbed rather than switched off for real: the setting is instance-wide, and
    // flipping it mid-run would make any other spec that registers fail depending on
    // timing. The API's own refusal is covered by the backend suite; what is worth
    // asserting here is that the client stops offering a door that no longer opens.
    await page.route('**/api/config', (route) =>
      route.fulfill({
        json: { localLoginEnabled: true, registrationEnabled: false, oidcAvailable: false, oidc: null },
      }),
    );

    await page.goto('/login');

    await expect(page.getByRole('link', { name: /create one/i })).toHaveCount(0);
    await expect(page.locator('input[type="password"]')).toHaveCount(1);
  });

  test('the client offers neither method when the instance accepts none', async ({ page }) => {
    await page.route('**/api/config', (route) =>
      route.fulfill({
        json: { localLoginEnabled: false, registrationEnabled: false, oidcAvailable: false, oidc: null },
      }),
    );

    await page.goto('/login');

    await expect(page.locator('input[type="password"]')).toHaveCount(0);
    await expect(page.getByText(/no sign-in method/i)).toBeVisible();
  });
});
