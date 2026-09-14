import { test, expect, type Page } from '@playwright/test';
import { injectSession, sessionFor, suiteAdmin } from './support/session';

/**
 * Snap-to-fill is the feature the README leads with and the only one that had no e2e
 * cover at all.
 *
 * What is exercised here is the wiring: the file the user picks reaches /api/coffees/scan,
 * what comes back lands in the right fields, and the two failure modes say so on screen.
 * The scan response is stubbed because what the OCR actually reads is a backend concern
 * and is tested there, TesseractCliOcrServiceTests drives a real engine process, and
 * CoffeeLabelParserTests covers the text-to-fields mapping against real label text. The
 * e2e run also sets Ocr__Engine=none, so a live scan here would only ever answer 503.
 */

/** A one-pixel PNG. The bytes never reach an OCR engine; only the upload path matters. */
const PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
  'base64',
);

/**
 * Adopts the administrator global setup already claimed, rather than registering a user
 * per test: /api/auth is rate-limited to 10/min, and four registrations here plus the
 * other specs' trip it, which is exactly why the suite seeds sessions instead of
 * signing in. Scanning needs an authenticated caller, nothing more.
 */
async function signIn(page: Page): Promise<void> {
  await injectSession(page, sessionFor(await suiteAdmin()));
}

/** Picks a bag photo on the Add Coffee form, which is what triggers a scan. */
async function snap(page: Page): Promise<void> {
  await page
    .locator('input[type="file"][capture]')
    .setInputFiles({ name: 'bag.png', mimeType: 'image/png', buffer: PNG });
}

test.describe('snap-to-fill', () => {
  test('a scanned bag pre-fills the form without saving anything', async ({ page }) => {
    await signIn(page);
    await page.route('**/api/coffees/scan', (route) =>
      route.fulfill({
        json: {
          rawText: 'La Cabra\nKirinyaga AA\nKenya\nLight roast',
          parsed: {
            name: 'Kirinyaga AA',
            roaster: 'La Cabra',
            origin: 'Kenya',
            roastLevel: 'light',
            weight: '250g',
          },
        },
      }),
    );

    await page.goto('/coffees/new');
    await snap(page);

    // Every field the scan recognised is filled in, and the roast free text ("light")
    // has been mapped onto the enum the select expects.
    await expect(page.getByLabel(/^name/i)).toHaveValue('Kirinyaga AA');
    await expect(page.getByLabel(/roaster/i)).toHaveValue('La Cabra');
    await expect(page.getByLabel(/origin/i)).toHaveValue('Kenya');
    await expect(page.getByLabel(/roast level/i)).toHaveValue('Light');

    // Scanning reads the bag; it does not create the coffee. The user still has to save.
    await expect(page).toHaveURL(/\/coffees\/new$/);
  });

  test('the form keeps what the user already typed over fields the scan missed', async ({
    page,
  }) => {
    await signIn(page);
    await page.route('**/api/coffees/scan', (route) =>
      route.fulfill({
        json: {
          rawText: 'illegible',
          // A real scan often recognises only some of the label.
          parsed: { name: null, roaster: 'La Cabra', origin: null, roastLevel: null, weight: null },
        },
      }),
    );

    await page.goto('/coffees/new');
    await page.getByLabel(/^name/i).fill('My own name');
    await snap(page);

    await expect(page.getByLabel(/roaster/i)).toHaveValue('La Cabra');
    // Overwriting this with an empty value would silently discard the user's typing.
    await expect(page.getByLabel(/^name/i)).toHaveValue('My own name');
  });

  test('an instance with OCR switched off says so instead of failing silently', async ({
    page,
  }) => {
    await signIn(page);
    // What a real deployment answers when Ocr__Engine=none, which is also what the e2e
    // backend is configured with.
    await page.route('**/api/coffees/scan', (route) =>
      route.fulfill({ status: 503, json: { detail: 'OCR is unavailable.' } }),
    );

    await page.goto('/coffees/new');
    await snap(page);

    await expect(page.getByText(/OCR is off on this host/i)).toBeVisible();
    // The form stays usable: the feature being off must not block typing a coffee in.
    await expect(page.getByLabel(/^name/i)).toBeEditable();
  });

  test('a rejected upload is reported rather than leaving the button spinning', async ({
    page,
  }) => {
    await signIn(page);
    await page.route('**/api/coffees/scan', (route) =>
      route.fulfill({ status: 400, json: { detail: 'Unsupported image type.' } }),
    );

    await page.goto('/coffees/new');
    await snap(page);

    await expect(page.getByText(/could not read that photo/i)).toBeVisible();
    // "Reading the label…" is the in-progress label; it must not be the resting state.
    await expect(page.getByText(/reading the label/i)).toBeHidden();
    await expect(page.getByText(/snap the bag/i)).toBeVisible();
  });
});
