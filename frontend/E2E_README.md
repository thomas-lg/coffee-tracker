# Playwright E2E Tests for Coffee Tracker

Quick start guide for running end-to-end tests.

## Quick Start

### 1. Prerequisites
- Backend running: `cd backend && dotnet run` (port 5000)
- Dependencies installed: `cd frontend && npm ci`

### 2. Run Tests
```bash
cd frontend

# Headless (CI mode)
npm run e2e

# Interactive UI (recommended for debugging)
npm run e2e:ui

# Visible browser
npm run e2e:headed

# Step-by-step debugging
npm run e2e:debug
```

### 3. View Results
```bash
# Open HTML report
npx playwright show-report
```

## Test Credentials

Tests use:
- **Email**: `test@example.com`
- **Password**: `password123`

Make sure this test account exists on your backend.

## Files

- **Config**: `playwright.config.ts`
- **Fixtures**: `e2e/fixtures/auth.fixture.ts` (auth helpers)
- **Tests**: `e2e/tests/*.spec.ts` (6 test suites)
- **Utilities**: `e2e/utils/test-helpers.ts`
- **CI**: `.github/workflows/ci.yml` (e2e job)
- **Docs**: `../docs/E2E.md` (detailed guide)

## Test Coverage

- ✅ **Auth**: Login, logout, token expiration, protected routes
- ✅ **Browse**: Search, filter, sort, pagination
- ✅ **Add/Edit**: Form validation, file upload, snap-to-fill
- ✅ **Detail**: View coffee, rate, see reviews
- ✅ **Theme**: Light/dark mode toggle
- ✅ **Integration**: Full user journeys

## Troubleshooting

**Tests timeout?**
- Check backend is running: `curl http://localhost:5000/api/health`
- Check frontend is serving: `http://localhost:4200`

**Session not found?**
- Verify test credentials exist on backend
- Run `npm run e2e:ui` to visually debug login

**Selector errors?**
- Run `npm run e2e:headed` to see the actual UI
- Update selectors in test files to match current UI

For detailed troubleshooting, see `../docs/E2E.md`.
