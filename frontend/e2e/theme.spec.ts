import { test, expect } from '@playwright/test';
import { injectSession, sessionFor, suiteAdmin } from './support/session';

/**
 * Theming is one of the things the README puts in a screenshot, and none of it was
 * covered: the toggle, the fact that a choice survives a reload, and the fact that
 * without a choice the OS preference decides.
 *
 * The assertions read `data-theme` on <html> rather than a computed colour, because that
 * attribute is what every CSS token in styles.css keys off — checking a rendered colour
 * would restate the palette in the test and break on any repaint.
 */

const THEME_KEY = 'ct.theme';

test.describe('theming', () => {
  test('the toggle flips the document theme and says what it will do next', async ({ page }) => {
    await injectSession(page, sessionFor(await suiteAdmin()));
    await page.goto('/coffees');

    const html = page.locator('html');
    const before = await html.getAttribute('data-theme');
    const toggle = page.getByRole('button', { name: /switch to (light|dark) mode/i });

    // The label names the destination, not the current state, so a screen-reader user
    // knows what pressing it does.
    await expect(toggle).toHaveAccessibleName(
      before === 'dark' ? /switch to light mode/i : /switch to dark mode/i,
    );

    await toggle.click();
    await expect(html).toHaveAttribute('data-theme', before === 'dark' ? 'light' : 'dark');
  });

  test('a chosen theme survives a reload', async ({ page }) => {
    await injectSession(page, sessionFor(await suiteAdmin()));
    await page.goto('/coffees');

    await page.getByRole('button', { name: /switch to (light|dark) mode/i }).click();
    const chosen = await page.locator('html').getAttribute('data-theme');

    await page.reload();

    // Persisted, and applied before the user sees the page — otherwise the app flashes
    // the wrong theme on every load.
    await expect(page.locator('html')).toHaveAttribute('data-theme', chosen!);
    expect(await page.evaluate((k) => localStorage.getItem(k), THEME_KEY)).toBe(chosen);
  });

  test('with no choice stored, the OS preference decides', async ({ browser }) => {
    // A fresh context so nothing is persisted, told to prefer dark.
    const context = await browser.newContext({ colorScheme: 'dark' });
    const page = await context.newPage();
    await injectSession(page, sessionFor(await suiteAdmin()));

    await page.goto('/coffees');

    await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
    expect(await page.evaluate((k) => localStorage.getItem(k), THEME_KEY)).toBeNull();

    await context.close();
  });

  test('an explicit choice outranks the OS preference', async ({ browser }) => {
    const context = await browser.newContext({ colorScheme: 'dark' });
    const page = await context.newPage();
    await injectSession(page, sessionFor(await suiteAdmin()));
    await page.addInitScript((k) => localStorage.setItem(k, 'light'), THEME_KEY);

    await page.goto('/coffees');

    // The OS says dark; the user said light. The user wins, or the setting is pointless.
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');

    await context.close();
  });
});
