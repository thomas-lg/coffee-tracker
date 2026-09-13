# Test plan — admin backup

A restore deletes the catalog, so the question every phase below asks is the same one:
can a file that is wrong in some way get far enough to destroy what is already there?

## Phase 1 — Automated suites

1. `dotnet test CoffeeTracker.sln` — 282 tests.
   - `BackupServiceTests` (14) drives every refusal through a fake store that records
     whether it was written to. `Nothing_is_written_when_a_later_coffee_is_the_invalid_one`
     is the one that matters: validation must run over the whole file first.
   - `EfBackupStoreTests` (7) run against real in-memory SQLite, because what is being
     checked is what a fake cannot show — that a restore replaces rather than appends, and
     that the review→coffee link and the tag join survive the trip.
2. `npm test` — 151 specs, 12 of them `BackupStore`.
3. `npx playwright test` — 30 specs. The export runs against the real API; the restore is
   stubbed, because the suite is `fullyParallel` and a real restore would delete the
   coffees another spec is halfway through asserting on.

## Phase 2 — A real round trip

The automated restore is stubbed at the browser, so the whole path was walked once against
a running instance holding two coffees and one review.

1. `GET /api/admin/backup` → 200, `Content-Disposition: attachment;
   filename="coffee-tracker-2026-09-13.json"`, format version 1, both coffees, the review
   under its coffee.
2. Restored with the review's tags rewritten to `["BERRY", "Smoky"]` → 200,
   `{"coffees":2,"reviews":1,"warnings":["Unknown flavour tag \"Smoky\" was skipped."]}`.
3. Re-exported: the tag came back as `Berry` — matched case-insensitively onto the seeded
   row rather than created.
4. Restored the same file again: still two coffees, not four.

## Phase 3 — Files that are refused

Each left the catalog exactly as it was.

1. `formatVersion: 2` → 400, *"That backup is format version 2; this instance reads
   version 1."*
2. A review rated `0` → 400, *"Coffee 2 (…) has a review rated 0; ratings are 1 to 5."* —
   the coffee is named, because a file can hold hundreds.
3. A `.json` that is not a backup, and a file that is not JSON at all → refused in the
   browser with nothing sent (covered by `backup.spec.ts`).

## Phase 4 — Authorization

1. `GET /api/admin/backup` with no token → 401.
2. `GET` and `POST` with a freshly registered non-admin token → 403 both.
3. The **Backup** tab is absent for a non-admin, whose `/admin` route is already guarded.
