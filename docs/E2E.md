# Playwright E2E Testing Guide

This document describes how to run and maintain the end-to-end tests for Coffee Tracker using Playwright.

## Overview

The e2e test suite covers key user journeys:

- **Auth** (`auth.spec.ts`): Login, register, logout, token expiration
- **Browse** (`coffee-browse.spec.ts`): Browse catalog, search, filter, sort
- **Add/Edit** (`coffee-add-edit.spec.ts`): Add new coffee, edit existing, form validation
- **Detail** (`coffee-detail.spec.ts`): View coffee details, rate, see reviews
- **Theme** (`theme.spec.ts`): Light/dark mode toggle and persistence
- **Integration** (`integration.spec.ts`): Full user journeys and cross-page navigation

## Prerequisites

- Node 22+ (same as frontend)
- npm 10.9.8+
- Backend API running on `http://localhost:5000`
- Frontend development server or production build on `http://localhost:4200`

## Running E2E Tests Locally

### 1. Install Dependencies

```bash
cd frontend
npm ci
```

### 2. Start the Backend

```bash
cd backend
dotnet run
```

The API will be available at `http://localhost:5000`.

### 3. Run Tests

In a new terminal, from the `frontend` directory:

```bash
# Standard headless test run
npm run e2e

# Interactive UI mode (recommended for debugging)
npm run e2e:ui

# Headless with visible browser window
npm run e2e:headed

# Debug mode with Playwright Inspector
npm run e2e:debug
```

### Test Credentials

Tests assume a test account exists on the backend:

- **Email**: `test@example.com`
- **Password**: `password123`

If this account doesn't exist, update the credentials in `e2e/tests/auth.spec.ts` and create the account on the backend.

## Test Structure

### Fixtures (`e2e/fixtures/`)

**auth.fixture.ts**
- `loginViaUI()`: Perform login through the UI
- `logoutViaUI()`: Perform logout through the UI
- `injectSession()`: Programmatically inject a session token (for skipping login in tests)
- `createValidSession()`: Create a mock valid session
- `createExpiredSession()`: Create a mock expired session
- Custom fixtures: `authenticatedPage` and `adminPage` (pre-authenticated pages)

**Usage:**
```typescript
import { test, expect, loginViaUI, injectSession } from '../fixtures/auth.fixture';

test('login flow', async ({ page }) => {
  const session = await loginViaUI(page, 'test@example.com', 'password123');
  expect(session.token).toBeTruthy();
});

test('authenticated pages', async ({ authenticatedPage: page }) => {
  // Page is already logged in
  await page.goto('/coffees');
});
```

### Utilities (`e2e/utils/`)

**test-helpers.ts**
- `waitForCoffeesRequest()`: Wait for and capture coffee list API calls
- `expectToast()`: Verify toast notifications
- `getTodayString()`: Get today's date in YYYY-MM-DD format
- `hasClass()`: Check if element has a CSS class

## Running in CI

The CI workflow (`.github/workflows/ci.yml`) includes an `e2e` job that:

1. Installs Node dependencies
2. Installs Playwright browsers
3. Builds the production frontend
4. Starts the backend
5. Runs all e2e tests
6. Uploads test artifacts (HTML report, videos, screenshots) on failure

The job uses `reuseExistingServer: false` in CI to ensure a clean state for each run.

## Debugging Failed Tests

### 1. Check the HTML Report

After tests run, an HTML report is generated:

```bash
# View the report
npx playwright show-report
```

This shows:
- All test results
- Screenshots on failure
- Video recordings of the test
- Network logs

### 2. Run a Single Test

```bash
# Run only auth.spec.ts
npx playwright test auth.spec.ts

# Run a specific test
npx playwright test auth.spec.ts -g "should login with valid credentials"
```

### 3. Use Debug Mode

```bash
npm run e2e:debug
```

This opens Playwright Inspector, allowing you to:
- Step through each action
- Inspect the DOM
- Modify selectors in real-time
- Replay steps

### 4. Check Screenshots/Videos

Failed tests automatically capture:
- **Screenshots**: `test-results/`
- **Videos**: `test-results/` (if `video: 'retain-on-failure'`)

## Selectors & Accessibility

Tests use accessible selectors (no `data-testid`):

- **`getByRole('button', { name: /text/i })`** — buttons by accessible name
- **`getByLabel(/email/i)`** — form fields by label text
- **`getByPlaceholderText()`** — inputs by placeholder
- **`locator('ct-coffee-card')`** — web components

This approach ensures tests reflect how real users interact with the app.

## Modifying Tests

### Adding a New Test File

1. Create `e2e/tests/my-feature.spec.ts`
2. Import the fixture:
   ```typescript
   import { test, expect } from '../fixtures/auth.fixture';
   ```
3. Write tests:
   ```typescript
   test.describe('My Feature', () => {
     test('should do something', async ({ authenticatedPage: page }) => {
       await page.goto('/my-page');
       // assertions...
     });
   });
   ```

### Updating Selectors

If UI changes (e.g., button text, label wording):

1. Find the test that uses the selector
2. Update the selector to match the new UI
3. Re-run: `npm run e2e:ui` to verify

### Test Data & API Mocking

Tests currently use the **real backend API**. If you need to isolate tests:

- Use `loginViaUI()` to create real test data via the API
- Or implement API mocking with MSW (Mock Service Worker) — see commented examples in config

## Configuration

### playwright.config.ts

Key settings:

- **baseURL**: `http://localhost:4200` (dev server or prod build)
- **webServer**: Starts `npm run start` before tests (dev server) or uncomment backend startup
- **timeout**: 30s per test
- **expect timeout**: 5s per assertion
- **retries**: 0 locally, 2 in CI
- **workers**: Parallel (auto) locally, 1 in CI (sequential)
- **reporters**: HTML report, list, and GitHub annotations in CI

To adjust timeouts:

```typescript
test('slow test', async ({ page }) => {
  // This test needs more time
  test.setTimeout(60 * 1000);
});
```

## Best Practices

### 1. Use Fixtures for Auth

```typescript
// ✅ Good: Use fixture
test('browse coffees', async ({ authenticatedPage: page }) => {
  await page.goto('/coffees');
});

// ❌ Avoid: Manual session injection
test('browse coffees', async ({ page }) => {
  // manual login every time...
});
```

### 2. Handle Conditional Content

Some features (e.g., origin filter) only show if data exists:

```typescript
// ✅ Good: Gracefully skip
const originSelect = page.getByLabel(/filter by origin/i);
const isVisible = await originSelect.isVisible().catch(() => false);
if (isVisible) {
  await expect(originSelect).toBeVisible();
}

// ❌ Avoid: Assuming it's always there
await expect(page.getByLabel(/filter by origin/i)).toBeVisible();
```

### 3. Wait for Async Operations

```typescript
// ✅ Good: Wait for network
await page.waitForURL('/coffees');
await page.waitForResponse(r => r.url().includes('/api/coffees'));

// ❌ Avoid: Hard sleeps
await page.waitForTimeout(5000); // Only if necessary
```

### 4. Group Related Tests

```typescript
test.describe('Coffee Grid', () => {
  test('should display search input', async ({ page }) => { });
  test('should filter by search term', async ({ page }) => { });
  test('should filter by roast level', async ({ page }) => { });
});
```

## Troubleshooting

### Tests timeout waiting for API

**Problem**: `page.waitForResponse()` or `page.waitForURL()` times out

**Solution**:
1. Check backend is running: `curl http://localhost:5000/api/health`
2. Increase timeout: `test.setTimeout(60 * 1000)`
3. Check network in DevTools

### Session not found in localStorage

**Problem**: `Error: Session not found in localStorage after login`

**Solution**:
1. Verify test credentials exist on backend
2. Check login form selectors match the UI
3. Run `npm run e2e:ui` to visually debug the login flow

### Flaky tests (intermittent failures)

**Problem**: Test passes sometimes, fails other times

**Solution**:
1. Add explicit waits: `page.waitForURL()`, `page.waitForLoadState()`
2. Avoid hard timeouts: `page.waitForTimeout(100)` can race with the app
3. Use `toBeVisible()` with timeout: `expect(el).toBeVisible({ timeout: 10000 })`

### Tests pass locally but fail in CI

**Common causes**:
- Different test data on CI backend
- Timing issues (CI runner is slower)
- Port conflicts (use available ports)

**Solution**:
1. Check CI logs: View artifact `playwright-report/`
2. Run tests locally with `CI=true npm run e2e`
3. Increase timeouts for CI: use `process.env.CI` to adjust

## Resources

- [Playwright Documentation](https://playwright.dev)
- [Accessibility Best Practices](https://playwright.dev/docs/locators#locate-by-role)
- [Debugging Guide](https://playwright.dev/docs/debug)
- [GitHub Actions Integration](https://playwright.dev/docs/ci)
