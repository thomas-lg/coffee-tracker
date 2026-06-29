import { Page, expect } from '@playwright/test';

/**
 * Wait for API request to `/api/coffees` and return the response.
 */
export async function waitForCoffeesRequest(page: Page): Promise<any> {
  const response = await page.waitForResponse(
    (r) => r.url().includes('/api/coffees') && r.request().method() === 'GET'
  );
  return response.json();
}

/**
 * Verify that a toast notification is shown with the given text.
 */
export async function expectToast(page: Page, text: string | RegExp): Promise<void> {
  const toast = page.locator('ct-toast');
  await expect(toast).toContainText(text);
}

/**
 * Extract the date input value (format: YYYY-MM-DD).
 */
export async function getTodayString(): string {
  const today = new Date();
  return today.toISOString().slice(0, 10);
}

/**
 * Check if an element has a specific CSS class.
 */
export async function hasClass(element: any, className: string): Promise<boolean> {
  const classes = await element.getAttribute('class');
  return classes ? classes.includes(className) : false;
}
