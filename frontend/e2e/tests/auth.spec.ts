import { test, expect, loginViaUI, logoutViaUI, injectSession, createExpiredSession } from '../fixtures/auth.fixture';

test.describe('Authentication', () => {
  test('should redirect to /login when unauthenticated', async ({ page }) => {
    // Try to access protected route without session
    await page.goto('/coffees');
    await page.waitForURL('/login');
    expect(page.url()).toContain('/login');
  });

  test('should login with valid credentials', async ({ page }) => {
    // This assumes a test account exists on the API
    const session = await loginViaUI(page, 'test@example.com', 'password123');
    expect(session.token).toBeTruthy();
    expect(session.userId).toBeTruthy();
    expect(page.url()).toContain('/');
  });

  test('should show error on invalid credentials', async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel(/email/i).fill('invalid@example.com');
    await page.getByLabel(/password/i).fill('wrongpassword');
    await page.getByRole('button', { name: /sign in/i }).click();

    // Should stay on /login and show error toast
    await expect(page.locator('ct-toast')).toContainText(/invalid email or password/i);
    expect(page.url()).toContain('/login');
  });

  test('should logout and clear session', async ({ authenticatedPage: page }) => {
    // Start authenticated
    expect(page.url()).toContain('/');

    // Logout
    await logoutViaUI(page);

    // Verify session cleared
    const sessionJson = await page.evaluate(() => localStorage.getItem('ct.session'));
    expect(sessionJson).toBeNull();
  });

  test('should detect expired token and prevent navigation to protected routes', async ({ page }) => {
    const expiredSession = createExpiredSession();
    await injectSession(page, expiredSession);

    // Try to navigate to protected route
    await page.goto('/coffees');

    // authGuard checks isAuthenticated() which returns false for expired tokens
    // Should redirect to /login
    await page.waitForURL('/login');
    expect(page.url()).toContain('/login');
  });

  test('should show login page with email and password fields', async ({ page }) => {
    await page.goto('/login');

    // Check for form fields
    await expect(page.getByLabel(/email/i)).toBeVisible();
    await expect(page.getByLabel(/password/i)).toBeVisible();
    await expect(page.getByRole('button', { name: /sign in/i })).toBeVisible();

    // Check for register link
    await expect(page.getByRole('link', { name: /create one/i })).toBeVisible();
  });

  test('should show welcome text on login page', async ({ page }) => {
    await page.goto('/login');
    await expect(page.getByText(/welcome back/i)).toBeVisible();
    await expect(page.getByRole('heading', { name: /sign in/i })).toBeVisible();
  });
});
