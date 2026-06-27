## 1. What counts as "used"

A photo is **used** iff its relative path equals some coffee's `PhotoPath`. Reviews
carry no photos, so coffees are the only referrers. Both sides speak the same path
shape — `SaveAsync` returns `photos/{guid}.ext` and that exact string is stored in
`Coffee.PhotoPath` — so the diff is a plain set membership test, no normalization.

## 2. Diff, not delete-all

`GET /api/admin/photos` returns every stored photo with a `used` flag (stored set
minus used set → unused). The operator reviews and picks what to remove; we never
auto-delete. This matches the agreed design from the M5 review.

## 3. Delete re-checks usage (no destructive race)

`DELETE /api/admin/photos` takes a list of paths. The service recomputes the used
set at delete time and deletes only requested paths that are **still unused**,
skipping any a coffee now references. This closes the window where a photo is
attached to a coffee between the list and the delete call — cleanup can never orphan
a live coffee of its image. The response reports how many were deleted vs skipped.
Deletion reuses `IPhotoStorage.DeleteAsync` (best-effort, idempotent).

## 4. Admin authorization at the boundary

This is the first admin-only endpoint group. Rather than scatter `IsAdmin` checks in
the service (the review-delete path does that because it's owner-OR-admin), we add a
reusable `Admin` authorization policy over the existing `isAdmin` claim and apply
`[Authorize(Policy = "Admin")]` to the controller. Non-admins get a `403` from the
framework before any handler runs; the service stays free of auth concerns.

## 5. ListAsync scope

The filesystem adapter enumerates top-level files in the photos directory and
returns `photos/{filename}` for each; a missing directory yields an empty list. It
does not recurse (storage writes flat) and ignores the directory's own WAL/temp
noise by listing files only.
