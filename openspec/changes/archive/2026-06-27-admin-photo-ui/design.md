## 1. A new feature package

Mirrors the `coffees` library layout (ng-packagr lib, `tsconfig.lib[.prod]`, spec,
public-api) and is wired via the root `tsconfig` path + an `angular.json` project, so
`ng build app` resolves it from source and `ng test @coffee-tracker/admin` runs its
specs. Kept separate from `coffees` because it is not catalogue functionality.

## 2. Selection is unused-only

Used photos render with a "Used" badge and are not selectable — the API already skips
them on delete, and disabling selection makes that guarantee visible rather than
letting the user select something that silently won't delete. `selectAllUnused` and
the counts derive from the `httpResource` list via computed signals.

## 3. Delete re-fetches; two-step confirm

`deleteSelected` awaits the `DELETE`, clears the selection, and `reload()`s the list
so the grid reflects reality (and any concurrently-attached photo now shows as used).
Deletion is armed by a two-step inline confirm (no modal primitive exists) and the
result is surfaced via the existing `ToastService` ("Deleted N, skipped M").

## 4. Card grid over a table

A selectable thumbnail grid (not a dense table) matches the app's warm card aesthetic
and is better on mobile — you want to see the photo you're about to delete.
