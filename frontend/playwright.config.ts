import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright e2e config for the Coffee Tracker SPA.
 *
 * Playwright starts ONLY the Angular dev server (`ng serve app` on :4200). The
 * backend API is expected to already be running on :5000 — `proxy.conf.json`
 * forwards `/api` and `/photos` there. CI starts the backend in a separate step
 * before invoking the tests; locally, run the backend yourself (see e2e/README
 * notes / the repo's local-run guidance — port 5000 is squatted by AirPlay on
 * macOS, so disable AirPlay Receiver or remap the backend + proxy).
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : [['list']],
  use: {
    baseURL: 'http://localhost:4200',
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'npm run start',
    url: 'http://localhost:4200',
    reuseExistingServer: !process.env.CI,
    timeout: 180_000,
  },
});
