## Why

The M5 OCR review flagged a storage leak: `POST /api/coffees/scan` stores the bag
photo and returns its path, but if the user never saves the coffee that file is
orphaned. Photos are also orphaned by any path that drifts from a coffee's
`PhotoPath`. Nothing reaps them today, so the photos volume grows unbounded on the
NAS. This adds an **admin-only** audit + delete path so an operator can review and
remove unused photos deliberately (not a blind "delete all unused").

## What Changes

- **`IPhotoStorage.ListAsync()`** (driven port + filesystem adapter): enumerate the
  relative paths of every stored photo.
- **`ICoffeeRepository.GetUsedPhotoPathsAsync()`** (driven port + EF adapter): the
  set of non-null `Coffee.PhotoPath` values, projected (no full entities).
- **`IPhotoAdminService`** (driving port + service): diff stored photos against used
  paths to mark each **used/unused**; delete a caller-selected set, but **only**
  paths that are still unused at delete time (skip any a coffee now references), so
  cleanup can never break a catalogued coffee.
- **`AdminPhotosController`**: `GET /api/admin/photos` (list with used flag) and
  `DELETE /api/admin/photos` (delete selected paths). **Admin-only.**
- **Admin authorization policy** in `Program.cs` (first admin-only endpoint group):
  a reusable `Admin` policy over the existing `isAdmin` claim; non-admins get `403`.

## Capabilities

### Modified Capabilities

- `photo-storage`: adds administrative auditing and selective deletion of stored
  photos, including orphans left by unsaved scans.

## Impact

- New driving port + service, two new driven-port methods + adapter impls, DTOs, a
  controller, and an `Admin` auth policy. No database/schema change. The bulk-select
  table UI is the frontend's concern (M6 follow-up), out of scope here.
