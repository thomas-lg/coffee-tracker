## Why

M4 deliberately enforced **one review per user per coffee**. But a bag of coffee
changes as it rests and degasses — a light roast can peak a week in, then fade. The
user wants to capture that: rate the **same coffee multiple times over its life**
("fresh bag", "mid-week", "last cup", or just whenever), with the rating kept
**simple**. This reverses the one-per-user rule and reframes a "review" as a dated
entry in a per-user timeline. It is the backend prerequisite for the M6 frontend's
"ratings over time" UI.

## What Changes

- **Drop the one-per-user constraint** (the `IX_Reviews_CoffeeId_UserId` *unique*
  index from M4) so a user may have **many `Review` rows** per coffee; keep a
  non-unique index on `(CoffeeId, UserId)` for query performance.
- Add an optional, free-form **`Stage`** (context) string to a review — e.g.
  "Fresh bag", "Mid-week", "Last cups". Rating stays 1–5; notes/brew/tags remain
  optional.
- `POST /api/coffees/{coffeeId}/reviews` **always creates a new dated entry** — no
  more `409` on a second submission.
- `GET /api/coffees/{coffeeId}/reviews` returns entries **newest-first** (per-user
  grouping is a client concern). Single-review GET/PUT/DELETE and ownership/admin
  rules are unchanged — you still edit or delete a specific entry you own; admins
  still moderate.
- Remove/repurpose `DuplicateReviewException` (no longer thrown).
- Re-verify `AverageRating`/`ReviewCount` on `CoffeeResponseDto`: average across
  **all** entries; document that count is entries, not distinct reviewers.

Out of scope (deferred): the M6 timeline UI; any "edit vs new entry" heuristic
(every POST is a new entry); per-stage analytics; collapsing historical data.

## Capabilities

### Modified Capabilities
- `reviews`: a user may record **many dated rating entries** per coffee (was: at
  most one), each optionally tagged with a stage.

## Impact

- **Modified projects:** `CoffeeTracker.Domain` (`Review.Stage`),
  `CoffeeTracker.Infrastructure` (index change + migration, repo ordering),
  `CoffeeTracker.Application` (DTOs add `Stage`, `ReviewService` drops duplicate
  rejection), `CoffeeTracker.Api` (no `409` duplicate mapping).
- **DB migration:** drop the unique `IX_Reviews_CoffeeId_UserId`, add a non-unique
  index on `(CoffeeId, UserId)`, add a nullable `Stage` column. Non-destructive —
  existing rows are preserved and each becomes that user's first entry.
- **Tests:** `ReviewServiceTests`/`EfRepositoryTests` assertions about the
  one-per-user `409` are inverted to assert multiple dated entries are allowed.
- **Backward compatibility:** existing data preserved; the `409` duplicate path
  disappears (only our own not-yet-built SPA depends on it).
