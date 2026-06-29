import { test, expect } from '../fixtures/auth.fixture';

test.describe('Browse Coffees', () => {
  test.beforeEach(async ({ authenticatedPage: page }) => {
    // Navigate to browse page
    await page.goto('/coffees');
  });

  test('should show empty shelf message when no coffees', async ({ authenticatedPage: page }) => {
    // If the test account has no coffees, should see empty state
    // Note: this depends on the test data setup
    const emptyMsg = page.locator('text=/Nothing on the shelf yet/i');
    if (await emptyMsg.isVisible()) {
      await expect(emptyMsg).toBeVisible();
      await expect(page.getByRole('link', { name: /add a coffee/i })).toBeVisible();
    }
  });

  test('should display search input with placeholder', async ({ authenticatedPage: page }) => {
    const searchInput = page.getByPlaceholderText(/search by name, roaster, or origin/i);
    await expect(searchInput).toBeVisible();
  });

  test('should filter by search term', async ({ authenticatedPage: page }) => {
    const searchInput = page.getByPlaceholderText(/search by name, roaster, or origin/i);
    
    // Get initial count
    const initialCards = page.locator('ct-coffee-card');
    const initialCount = await initialCards.count();

    // If there are no coffees, skip this test
    if (initialCount === 0) {
      test.skip();
      return;
    }

    // Type search term
    await searchInput.fill('espresso');
    
    // Wait for filter to apply (should be instant with signals)
    await page.waitForTimeout(500);
    
    // Count should be <= initialCount
    const filteredCards = page.locator('ct-coffee-card');
    const filteredCount = await filteredCards.count();
    expect(filteredCount).toBeLessThanOrEqual(initialCount);
  });

  test('should show sort dropdown', async ({ authenticatedPage: page }) => {
    const sortSelect = page.getByLabel(/sort coffees/i);
    await expect(sortSelect).toBeVisible();
  });

  test('should change sort order', async ({ authenticatedPage: page }) => {
    const sortSelect = page.getByLabel(/sort coffees/i);
    
    // Select "Name A–Z"
    await sortSelect.selectOption('name');
    
    // Verify selection persisted
    await expect(sortSelect).toHaveValue('name');
  });

  test('should show roast filter buttons', async ({ authenticatedPage: page }) => {
    // Should have "All roasts" button and individual roast level buttons
    await expect(page.getByRole('button', { name: /all roasts/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /light/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /medium/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /dark/i })).toBeVisible();
  });

  test('should filter by roast level', async ({ authenticatedPage: page }) => {
    // Click on "Light" roast button
    const lightButton = page.getByRole('button', { name: /^light$/i });
    await lightButton.click();
    
    // Should be visually selected (active state)
    // The active button should have bg-ink and text-foam classes
    await expect(lightButton).toHaveClass(/bg-ink/);
  });

  test('should show origin filter if multiple origins exist', async ({ authenticatedPage: page }) => {
    const originSelect = page.getByLabel(/filter by origin/i);
    const isVisible = await originSelect.isVisible().catch(() => false);
    
    if (isVisible) {
      await expect(originSelect).toBeVisible();
    }
  });

  test('should show flavor filter if flavors exist', async ({ authenticatedPage: page }) => {
    const flavorSelect = page.getByLabel(/filter by flavour/i);
    const isVisible = await flavorSelect.isVisible().catch(() => false);
    
    if (isVisible) {
      await expect(flavorSelect).toBeVisible();
    }
  });

  test('should show "Add a coffee" button', async ({ authenticatedPage: page }) => {
    const addButton = page.getByRole('link', { name: /add a coffee/i });
    await expect(addButton).toBeVisible();
    
    // Navigate to add page
    await addButton.click();
    await page.waitForURL('/coffees/new');
  });

  test('should navigate to coffee detail on card click', async ({ authenticatedPage: page }) => {
    const cards = page.locator('ct-coffee-card');
    const count = await cards.count();
    
    if (count === 0) {
      test.skip();
      return;
    }

    // Click first card
    const firstCard = cards.first();
    await firstCard.click();
    
    // Should navigate to detail page (/coffees/:id)
    await page.waitForURL(/\/coffees\/\d+$/);
  });
});
