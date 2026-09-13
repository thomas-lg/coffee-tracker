## 1. The file format

- [x] 1.1 `BackupDto` with a `FormatVersion` constant, an export timestamp and the coffees
- [x] 1.2 `BackupCoffeeDto` / `BackupReviewDto` without ids — a restore creates new rows
- [x] 1.3 Carry flavour tags by name rather than id
- [x] 1.4 `ImportOutcome` separating "unsupported format" from "invalid contents", each with a reason

## 2. Application layer

- [x] 2.1 `IBackupService` (driving) and `IBackupStore` (driven)
- [x] 2.2 `BackupService.ExportAsync` maps the catalog onto the file format
- [x] 2.3 `BackupService.ImportAsync` validates the whole file before the store is touched
- [x] 2.4 Refuse a version mismatch naming both versions, so "upgrade first" is actionable
- [x] 2.5 Register both in the DI extensions

## 3. Persistence

- [x] 3.1 `EfBackupStore.ExportAsync` reads coffees and reviews in two queries, not one per coffee
- [x] 3.2 `ReplaceAllAsync` deletes and re-inserts inside a single transaction
- [x] 3.3 Insert coffees and save before the reviews, which carry a plain `CoffeeId`
- [x] 3.4 Match tags by name, case-insensitively; skip and report an unknown one

## 4. API

- [x] 4.1 `AdminBackupController` under the administrator policy
- [x] 4.2 GET sets `Content-Disposition` with a dated filename
- [x] 4.3 POST maps both refusals to 400 Problem carrying the reason

## 5. Client

- [x] 5.1 `AdminBackupApi`, and the four models with their compile-time parity guards
- [x] 5.2 `BackupStore`: export to a blob download, staged file, confirm, restore
- [x] 5.3 Parse and shape-check the chosen file in the browser
- [x] 5.4 `BackupScreen` plus the `backup` route and the admin tab
- [x] 5.5 Reload the catalog after a restore — the shelf is a different shelf
- [x] 5.6 Show the counts and any skipped tags after a restore, not just a toast

## 6. Tests

- [x] 6.1 `BackupServiceTests` — refusal happens before anything is written
- [x] 6.2 `EfBackupStoreTests` against real SQLite — round trip, replace, tag join
- [x] 6.3 `backup.store.spec.ts` — staging, refusals, the API reason, no double submit
- [x] 6.4 `backup.spec.ts` e2e — a real download, and nothing posted before confirmation
- [x] 6.5 Extend the admin-tabs e2e to the third tab
