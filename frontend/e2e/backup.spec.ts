import { test, expect, type Page } from '@playwright/test';
import { injectSession, sessionFor, suiteAdmin } from './support/session';

/**
 * Export and restore of the whole catalog.
 *
 * The export runs against the real API: it reads, so it costs the suite nothing. The
 * restore does not, because it *replaces* the catalog of the instance under test and
 * the suite is fullyParallel, a real restore would delete the coffees another spec is
 * halfway through asserting on. So the POST is stubbed and what is asserted is the
 * screen's own contract: what it sends, and that it sends nothing before the user has
 * confirmed a destructive action.
 */

const BACKUP = {
  formatVersion: 1,
  exportedAt: '2026-09-13T12:00:00+00:00',
  coffees: [
    {
      name: 'Kirinyaga AA',
      roaster: 'La Cabra',
      origin: 'Kenya',
      roastLevel: 0,
      price: 18.5,
      dateBought: '2026-08-28',
      photoPath: null,
      notes: null,
      brewMethod: null,
      createdByUserId: 'restored',
      createdAt: '2026-08-28T00:00:00+00:00',
      reviews: [],
    },
  ],
};

async function signInAsAdmin(page: Page): Promise<void> {
  const admin = await suiteAdmin();
  await injectSession(page, sessionFor(admin, { isAdmin: true }));
}

/** Attaches a file to the screen's hidden input, as the OS picker would. */
async function chooseBackup(page: Page, name: string, body: string): Promise<void> {
  await page.locator('input[type=file]').setInputFiles({
    name,
    mimeType: 'application/json',
    buffer: Buffer.from(body),
  });
}

test.describe('backup', () => {
  test('an administrator can download the catalog as a file', async ({ page }) => {
    await signInAsAdmin(page);
    await page.goto('/admin/backup');

    const download = page.waitForEvent('download');
    await page.getByRole('button', { name: /download backup/i }).click();

    // The response is fetched with the bearer token and turned into a blob, so the only
    // proof the user actually gets a file is the download the anchor starts.
    expect((await download).suggestedFilename()).toMatch(
      /^coffee-tracker-\d{4}-\d{2}-\d{2}\.json$/,
    );
  });

  test('a file that is not a backup is refused before anything is sent', async ({ page }) => {
    await signInAsAdmin(page);
    let posted = false;
    await page.route('**/api/admin/backup', async (route) => {
      if (route.request().method() === 'POST') posted = true;
      await route.fallback();
    });

    await page.goto('/admin/backup');
    await chooseBackup(page, 'holiday.json', '{"photos":[]}');

    await expect(page.getByRole('alert')).toContainText(/not a coffee tracker backup/i);
    // No confirm row: a file the screen cannot read never gets as far as arming.
    await expect(page.getByRole('button', { name: /replace catalog/i })).toBeHidden();
    expect(posted).toBe(false);
  });

  test('choosing a backup only arms a confirmation, and cancelling drops it', async ({ page }) => {
    await signInAsAdmin(page);
    let posted = false;
    await page.route('**/api/admin/backup', async (route) => {
      if (route.request().method() === 'POST') posted = true;
      await route.fallback();
    });

    await page.goto('/admin/backup');
    await chooseBackup(page, 'shelf.json', JSON.stringify(BACKUP));

    // The prompt names the file and the size of what it is about to overwrite with.
    await expect(page.getByText(/replace the catalog with 1 coffee\(s\) from/i)).toBeVisible();
    await expect(page.getByText('shelf.json')).toBeVisible();
    expect(posted).toBe(false);

    await page.getByRole('button', { name: /^cancel$/i }).click();

    await expect(page.getByRole('button', { name: /replace catalog/i })).toBeHidden();
    expect(posted).toBe(false);
  });

  test('confirming sends the chosen file and reports what was written', async ({ page }) => {
    await signInAsAdmin(page);
    let sent: unknown = null;
    await page.route('**/api/admin/backup', async (route) => {
      if (route.request().method() !== 'POST') return route.fallback();
      sent = route.request().postDataJSON();
      await route.fulfill({
        json: { coffees: 1, reviews: 0, warnings: ['Unknown flavour tag "Smoky" was skipped.'] },
      });
    });

    await page.goto('/admin/backup');
    await chooseBackup(page, 'shelf.json', JSON.stringify(BACKUP));
    await page.getByRole('button', { name: /replace catalog/i }).click();

    await expect(page.getByText(/restored 1 coffee\(s\) and 0 review\(s\)/i)).toBeVisible();
    // Warnings are shown rather than folded into the toast: a skipped tag is a silent
    // difference between the file and what the instance now holds.
    await expect(page.getByText(/smoky/i)).toBeVisible();
    // The file travels verbatim; the screen validates but does not rewrite it.
    expect(sent).toEqual(BACKUP);
  });
});
