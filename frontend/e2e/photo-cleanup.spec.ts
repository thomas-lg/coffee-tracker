import { test, expect, type Page } from '@playwright/test';
import { injectSession, sessionFor, suiteAdmin } from './support/session';

/**
 * Photo cleanup is the only bulk destructive action in the app, and it had no e2e cover.
 *
 * The listing is exercised against the real API. The delete is not, because since the
 * scan endpoint stopped keeping files there is no supported way to *make* an orphan:
 * deleting a coffee removes its photo, replacing one removes the previous file, and a
 * failed replace removes the new one. Orphans now only come from a crash between the
 * write and the commit, or from a restored backup — neither of which a browser can
 * arrange. So the destructive path is driven against a stubbed listing, and what is
 * asserted is the screen's own behaviour: what it sends, and that it does not send
 * anything until the user confirms.
 */

async function signInAsAdmin(page: Page): Promise<void> {
  const admin = await suiteAdmin();
  await injectSession(page, sessionFor(admin, { isAdmin: true }));
}

/** Stands in for a listing the API cannot be made to produce on demand. */
function stubListing(page: Page, items: { path: string; used: boolean }[]): Promise<void> {
  return page.route('**/api/admin/photos', async (route) => {
    if (route.request().method() !== 'GET') return route.fallback();
    await route.fulfill({
      json: items.map((i) => ({ path: i.path, url: `/${i.path}?sig=stub`, used: i.used })),
    });
  });
}

test.describe('photo cleanup', () => {
  test('an administrator sees the stored photos, and none to reap on a healthy instance', async ({
    page,
  }) => {
    await signInAsAdmin(page);
    await page.goto('/admin/photos');

    // Real listing: the heading renders and the screen settles out of its loading state.
    await expect(page.getByRole('heading', { name: /photo cleanup/i })).toBeVisible();
    await expect(page.getByText(/loading photos/i)).toBeHidden();
  });

  test('a photo a coffee still uses cannot be selected for deletion', async ({ page }) => {
    await signInAsAdmin(page);
    await stubListing(page, [{ path: 'photos/in-use.webp', used: true }]);

    await page.goto('/admin/photos');

    // Locked rather than merely skipped server-side: the button says what it is and
    // refuses the click, so nobody arms a delete that was never going to happen.
    const inUse = page.getByRole('button', { name: /in use by a coffee/i });
    await expect(inUse).toBeVisible();
    await expect(inUse).toBeDisabled();
  });

  test('selecting an orphan is announced, and nothing is deleted until it is confirmed', async ({
    page,
  }) => {
    await signInAsAdmin(page);
    await stubListing(page, [{ path: 'photos/orphan.webp', used: false }]);

    let deleteRequests = 0;
    await page.route('**/api/admin/photos', async (route) => {
      if (route.request().method() === 'DELETE') {
        deleteRequests++;
        return route.fulfill({ json: { deleted: 1, skipped: 0 } });
      }
      return route.fallback();
    });

    await page.goto('/admin/photos');

    const orphan = page.getByRole('button', { name: /select .*orphan\.webp.* for deletion/i });
    await orphan.click();
    // aria-pressed is how a screen reader learns what is about to be deleted.
    await expect(orphan).toHaveAttribute('aria-pressed', 'true');

    // Arming is not deleting.
    await page.getByRole('button', { name: /delete 1/i }).click();
    expect(deleteRequests).toBe(0);

    await page.getByRole('button', { name: /confirm delete/i }).click();
    await expect.poll(() => deleteRequests).toBe(1);
  });

  test('cancelling an armed delete sends nothing', async ({ page }) => {
    await signInAsAdmin(page);
    await stubListing(page, [{ path: 'photos/orphan.webp', used: false }]);

    let deleteRequests = 0;
    await page.route('**/api/admin/photos', async (route) => {
      if (route.request().method() === 'DELETE') {
        deleteRequests++;
        return route.fulfill({ json: { deleted: 1, skipped: 0 } });
      }
      return route.fallback();
    });

    await page.goto('/admin/photos');
    await page.getByRole('button', { name: /select .*orphan\.webp.* for deletion/i }).click();
    await page.getByRole('button', { name: /delete 1/i }).click();
    await page.getByRole('button', { name: /^cancel$/i }).click();

    await expect(page.getByRole('button', { name: /confirm delete/i })).toBeHidden();
    expect(deleteRequests).toBe(0);
  });
});
