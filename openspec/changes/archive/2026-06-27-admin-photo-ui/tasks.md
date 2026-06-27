## 1. Data layer

- [x] 1.1 `AdminPhotosApi` (`list`, `delete(paths)` with body) + `PhotoListItem`/`PhotoDeleteResult` models
- [x] 1.2 Export from `@coffee-tracker/data` public API

## 2. Admin library

- [x] 2.1 Scaffold `@coffee-tracker/admin` (package.json, ng-package, tsconfigs, public-api, angular.json, tsconfig path, lockfile)
- [x] 2.2 `adminGuard` (redirects non-admins) + `ADMIN_ROUTES`
- [x] 2.3 `PhotoCleanupStore` (httpResource + selection/filter signals, delete+reload)
- [x] 2.4 `PhotoCleanup` component: selectable card grid, used/unused badges, two-step confirm, states

## 3. App wiring

- [x] 3.1 Lazy `/admin` route behind auth + admin guards
- [x] 3.2 Admin nav entry shown only when `isAdmin()`

## 4. Tests & CI

- [x] 4.1 `AdminPhotosApi` spec (GET + DELETE-with-body)
- [x] 4.2 `PhotoCleanupStore` spec (counts, toggle, select-all-unused, filter, delete+reload)
- [x] 4.3 Add `@coffee-tracker/admin` to the CI frontend test list

## 5. Verify

- [x] 5.1 `ng build app --configuration production` green
- [x] 5.2 `ng test` green for `admin` (5) and `data` (3)
