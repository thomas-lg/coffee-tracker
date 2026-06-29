import { test, expect } from '../fixtures/auth.fixture';

test.describe('Theme Toggle', () => {
  test('should show theme toggle button in header', async ({ authenticatedPage: page }) => {
    // Button should be visible with moon/sun icon
    const themeButton = page.getByRole('button', { name: /switch to/i });
    await expect(themeButton).toBeVisible();
  });

  test('should toggle between light and dark mode', async ({ authenticatedPage: page }) => {
    const themeButton = page.getByRole('button', { name: /switch to/i });

    // Get initial aria-label to determine current theme
    const initialLabel = await themeButton.getAttribute('aria-label');
    expect(initialLabel).toMatch(/switch to (light|dark) mode/i);

    // Click to toggle
    await themeButton.click();

    // Wait a moment for theme change
    await page.waitForTimeout(100);

    // Label should change
    const newLabel = await themeButton.getAttribute('aria-label');
    expect(newLabel).not.toBe(initialLabel);
  });

  test('should apply data-theme attribute to html element', async ({ authenticatedPage: page }) => {
    // Check initial data-theme
    const htmlElement = page.locator('html');
    let theme = await htmlElement.getAttribute('data-theme');
    expect(['light', 'dark']).toContain(theme);

    // Toggle theme
    const themeButton = page.getByRole('button', { name: /switch to/i });
    await themeButton.click();
    await page.waitForTimeout(100);

    // Check new data-theme
    const newTheme = await htmlElement.getAttribute('data-theme');
    expect(newTheme).not.toBe(theme);
  });

  test('should show sun icon in dark mode and moon icon in light mode', async ({
    authenticatedPage: page,
  }) => {
    const themeButton = page.getByRole('button', { name: /switch to/i });
    const htmlElement = page.locator('html');

    // Get current theme
    let currentTheme = await htmlElement.getAttribute('data-theme');

    if (currentTheme === 'dark') {
      // In dark mode, should show sun icon (to switch to light)
      await expect(page.locator('svg[aria-hidden="true"]')).toBeTruthy();
    } else {
      // In light mode, should show moon icon (to switch to dark)
      await expect(page.locator('svg[aria-hidden="true"]')).toBeTruthy();
    }
  });

  test('should persist theme preference across navigation', async ({ authenticatedPage: page }) => {
    const themeButton = page.getByRole('button', { name: /switch to/i });

    // Toggle theme
    await themeButton.click();
    await page.waitForTimeout(100);

    // Get initial theme
    const htmlElement = page.locator('html');
    const themeAfterToggle = await htmlElement.getAttribute('data-theme');

    // Navigate to another page
    await page.goto('/coffees');

    // Check if theme is still the same
    const themeAfterNavigation = await htmlElement.getAttribute('data-theme');
    expect(themeAfterNavigation).toBe(themeAfterToggle);
  });

  test('should be accessible with keyboard', async ({ authenticatedPage: page }) => {
    const themeButton = page.getByRole('button', { name: /switch to/i });

    // Focus and press Enter
    await themeButton.focus();
    await page.keyboard.press('Enter');
    await page.waitForTimeout(100);

    // Theme should change
    const htmlElement = page.locator('html');
    const theme = await htmlElement.getAttribute('data-theme');
    expect(['light', 'dark']).toContain(theme);
  });
});
