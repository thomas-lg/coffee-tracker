## Why

The admin photo-cleanup backend (`GET/DELETE /api/admin/photos`) shipped, but there
was no UI to drive it. This adds the admin-only screen so an operator can review
stored photos and reap orphans from the browser — the last user-facing piece of the
M5-review follow-up.

## What Changes

- **New `@coffee-tracker/admin` library** (feature = package, like `coffees`): an
  `adminGuard`, a page-scoped `PhotoCleanupStore` (`httpResource` + selection/filter
  signals), and a `PhotoCleanup` component (selectable card grid of photos with
  used/unused badges, two-step confirm delete, loading/empty/error states).
- **`@coffee-tracker/data`**: an `AdminPhotosApi` (`list()` / `delete(paths)`) plus
  `PhotoListItem` / `PhotoDeleteResult` model types.
- **App wiring**: lazy `/admin` route (auth- + admin-guarded) and an **Admin** nav
  entry shown only when `isAdmin()`.
- **CI**: the frontend job runs the new package's vitest suite.

## Capabilities

### Modified Capabilities

- `web-client`: adds an admin-only photo-cleanup screen over the existing admin photo
  endpoints.

## Impact

- New frontend library + a data API; lazy route + nav; no backend or schema change.
- The `PhotoListItem`/`PhotoDeleteResult` model types carry no `api-types.ts` drift
  guard yet (the endpoints post-date the last `gen:api`); re-run `npm run gen:api`
  once the OpenAPI doc includes `/api/admin/photos`.
