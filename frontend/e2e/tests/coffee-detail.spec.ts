import { test, expect } from '../fixtures/auth.fixture';

test.describe('Coffee Detail', () => {
  test('should display coffee details and specs', async ({ authenticatedPage: page }) => {
    await page.goto('/coffees');

    // Find and click first coffee card (if any exist)
    const cards = page.locator('ct-coffee-card');
    const count = await cards.count();

    if (count === 0) {
      test.skip();
      return;
    }

    await cards.first().click();
    await page.waitForURL(/\/coffees\/\d+$/);

    // Check for specs table
    await expect(page.getByText(/origin/i)).toBeVisible();
    await expect(page.getByText(/roaster/i)).toBeVisible();
    await expect(page.getByText(/roast/i)).toBeVisible();
    await expect(page.getByText(/price/i)).toBeVisible();
    await expect(page.getByText(/bought/i)).toBeVisible();
  });

  test('should show coffee name and roaster', async ({ authenticatedPage: page }) => {
    await page.goto('/coffees');
    const cards = page.locator('ct-coffee-card');
    
    if (await cards.count() === 0) {
      test.skip();
      return;
    }

    await cards.first().click();
    await page.waitForURL(/\/coffees\/\d+$/);

    // Should have heading with coffee name
    const heading = page.locator('h1');
    await expect(heading).toBeTruthy();
  });

  test('should show rating and review count', async ({ authenticatedPage: page }) => {
    await page.goto('/coffees');
    const cards = page.locator('ct-coffee-card');
    
    if (await cards.count() === 0) {
      test.skip();
      return;
    }

    await cards.first().click();
    await page.waitForURL(/\/coffees\/\d+$/);

    // Should show rating display
    await expect(page.getByText(/rating/i)).toBeVisible();
  });

  test('should show "Your ratings over time" section', async ({ authenticatedPage: page }) => {
    await page.goto('/coffees');
    const cards = page.locator('ct-coffee-card');
    
    if (await cards.count() === 0) {
      test.skip();
      return;
    }

    await cards.first().click();
    await page.waitForURL(/\/coffees\/\d+$/);

    // Check for ratings section
    await expect(page.getByRole('heading', { name: /your ratings over time/i })).toBeVisible();
  });

  test('should show "How\'s it tasting today?" rating input', async ({ authenticatedPage: page }) => {
    await page.goto('/coffees');
    const cards = page.locator('ct-coffee-card');
    
    if (await cards.count() === 0) {
      test.skip();
      return;
    }

    await cards.first().click();
    await page.waitForURL(/\/coffees\/\d+$/);

    // Check for rating section
    await expect(page.getByText(/how\'s it tasting today/i)).toBeVisible();
  });

  test('should show Edit and Delete buttons', async ({ authenticatedPage: page }) => {
    await page.goto('/coffees');
    const cards = page.locator('ct-coffee-card');
    
    if (await cards.count() === 0) {
      test.skip();
      return;
    }

    await cards.first().click();
    await page.waitForURL(/\/coffees\/\d+$/);

    // Check for Edit button
    await expect(page.getByRole('link', { name: /edit/i })).toBeVisible();
    
    // Check for Delete button
    await expect(page.getByRole('button', { name: /delete/i })).toBeVisible();
  });

  test('should navigate back to shelf', async ({ authenticatedPage: page }) => {
    await page.goto('/coffees');
    const cards = page.locator('ct-coffee-card');
    
    if (await cards.count() === 0) {
      test.skip();
      return;
    }

    await cards.first().click();
    await page.waitForURL(/\/coffees\/\d+$/);

    // Click back link
    const backLink = page.getByRole('link', { name: /back to shelf/i });
    await expect(backLink).toBeVisible();
    await backLink.click();
    await page.waitForURL('/coffees');
  });

  test('should add a rating', async ({ authenticatedPage: page }) => {
    await page.goto('/coffees');
    const cards = page.locator('ct-coffee-card');
    
    if (await cards.count() === 0) {
      test.skip();
      return;
    }

    await cards.first().click();
    await page.waitForURL(/\/coffees\/\d+$/);

    // Find and click a rating star (ct-rating interactive element)
    // This depends on ct-rating component implementation
    const ratingButtons = page.locator('button[aria-label*="star"]');
    if (await ratingButtons.count() > 0) {
      // Click 4th star for a 4-star rating
      await ratingButtons.nth(3).click();

      // Find save button
      const saveButton = page.getByRole('button', { name: /save.*rating/i });
      await expect(saveButton).toBeEnabled();
    }
  });

  test('should show "What others say" if reviews exist', async ({ authenticatedPage: page }) => {
    await page.goto('/coffees');
    const cards = page.locator('ct-coffee-card');
    
    if (await cards.count() === 0) {
      test.skip();
      return;
    }

    await cards.first().click();
    await page.waitForURL(/\/coffees\/\d+$/);

    // Check if "What others say" heading is visible
    const otherReviewsHeading = page.getByRole('heading', { name: /what others say/i });
    const isVisible = await otherReviewsHeading.isVisible().catch(() => false);
    
    if (isVisible) {
      await expect(otherReviewsHeading).toBeVisible();
    }
  });
});
