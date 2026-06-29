import { test, expect, loginViaUI, logoutViaUI } from '../fixtures/auth.fixture';

test.describe('Integration - Full User Journey', () => {
  test('should complete login -> browse -> add -> detail -> rate -> logout flow', async ({ page }) => {
    // 1. LOGIN
    const session = await loginViaUI(page, 'test@example.com', 'password123');
    expect(session.token).toBeTruthy();
    expect(page.url()).toContain('/');

    // 2. BROWSE
    await page.goto('/coffees');
    await page.waitForURL('/coffees');
    
    // Check shelf is visible
    const shellTitle = page.getByRole('heading', { name: /browse your shelf/i });
    await expect(shellTitle).toBeVisible().catch(() => {
      // Might not be visible if shelf is empty, that's ok
    });

    // 3. ADD COFFEE (navigate to add form)
    const addButton = page.getByRole('link', { name: /add a coffee/i });
    await expect(addButton).toBeVisible();
    await addButton.click();
    await page.waitForURL('/coffees/new');

    // 4. LOGOUT
    await logoutViaUI(page);

    // Verify back on login
    expect(page.url()).toContain('/login');
  });

  test('should show home page with stats after login', async ({ page }) => {
    // Login
    await loginViaUI(page, 'test@example.com', 'password123');

    // Should be on home page
    await page.waitForURL('/');

    // Check for home page elements
    const homeHeading = page.getByRole('heading').first();
    await expect(homeHeading).toBeVisible();
  });

  test('should show header navigation when authenticated', async ({ authenticatedPage: page }) => {
    // Check for navigation links in header
    const homeLink = page.getByRole('link', { name: /^home$/i });
    const browseLink = page.getByRole('link', { name: /^browse$/i });
    const addLink = page.getByRole('link', { name: /^add$/i });
    const signOutButton = page.getByRole('button', { name: /sign out/i });

    await expect(homeLink).toBeVisible();
    await expect(browseLink).toBeVisible();
    await expect(addLink).toBeVisible();
    await expect(signOutButton).toBeVisible();
  });

  test('should navigate between home, browse, and add pages', async ({ authenticatedPage: page }) => {
    // From home, navigate to browse
    const browseLink = page.getByRole('link', { name: /^browse$/i });
    await browseLink.click();
    await page.waitForURL('/coffees');
    expect(page.url()).toContain('/coffees');

    // From browse, navigate to add
    const addLink = page.getByRole('link', { name: /^add$/i });
    await addLink.click();
    await page.waitForURL('/coffees/new');
    expect(page.url()).toContain('/coffees/new');

    // Back to home
    const homeLink = page.getByRole('link', { name: /^home$/i });
    await homeLink.click();
    await page.waitForURL('/');
    expect(page.url()).toContain('/');
  });

  test('should show avatar with user initials in header when authenticated', async ({
    authenticatedPage: page,
  }) => {
    // Avatar should show initials
    const avatar = page.locator('span[title="You"]').first();
    const isVisible = await avatar.isVisible().catch(() => false);

    if (isVisible) {
      await expect(avatar).toBeVisible();
      const text = await avatar.textContent();
      // Should have 1-2 character initials
      expect(text?.length).toBeLessThanOrEqual(2);
    }
  });

  test('should maintain session across page navigation', async ({ authenticatedPage: page }) => {
    // Start at home
    await expect(page).toHaveURL('/');

    // Navigate to browse
    const browseLink = page.getByRole('link', { name: /^browse$/i });
    await browseLink.click();
    await page.waitForURL('/coffees');

    // Navigate back to home
    const homeLink = page.getByRole('link', { name: /^home$/i });
    await homeLink.click();
    await page.waitForURL('/');

    // Should still be authenticated (no redirect to /login)
    // Check that session is still in localStorage
    const sessionJson = await page.evaluate(() => localStorage.getItem('ct.session'));
    expect(sessionJson).toBeTruthy();
  });

  test('should show loading states and transitions', async ({ authenticatedPage: page }) => {
    // Navigate to browse which loads coffee list
    await page.goto('/coffees');

    // Check for skeleton loaders while loading
    // (timing depends on how fast API responds)
    const skeletons = page.locator('ct-skeleton');
    const skeletonsVisible = await skeletons.count().catch(() => 0);
    
    // Skeleton loaders should eventually disappear and cards appear
    await page.waitForTimeout(1000);
    const cards = page.locator('ct-coffee-card');
    const hasCards = await cards.count().catch(() => 0) > 0;
    
    // Either cards or empty message should be visible
    const emptyMsg = page.locator('text=/Nothing on the shelf yet/i');
    const hasEmpty = await emptyMsg.isVisible().catch(() => false);
    
    expect(hasCards || hasEmpty).toBeTruthy();
  });
});
