## 1. A "review" becomes a dated entry (keep one entity)

Reuse the existing `Review` entity rather than introducing a separate
`RatingEntry`. Its fields already fit (rating, `CreatedAt`, optional
notes/brew/tags); we just drop the uniqueness rule and add an optional `Stage`.
This means no data migration of existing rows, and the M6 timeline is simply "this
user's reviews for this coffee, newest first". The alternative — a lightweight
`RatingEntry` distinct from a heavier `Review` — was rejected as over-modeling
against the user's "keep it simple".

## 2. Drop the unique index; keep a non-unique one

M4's `AddReviews` migration created `IX_Reviews_CoffeeId_UserId` as **UNIQUE**. We
drop the uniqueness but keep a **non-unique** index on `(CoffeeId, UserId)` so
"my entries for this coffee" and per-coffee listing stay fast. `CreatedAt` drives
ordering (newest-first).

## 3. Optional `Stage`, free-form

`Stage` is a nullable string (max 40 chars), not an enum — the user said
"whenever" and wants it simple. The SPA can suggest values ("Fresh bag",
"Mid-week", "Last cups") but the API accepts any short string or null. No
validation beyond length.

## 4. POST always creates; no duplicate rejection

`POST` creates a new entry unconditionally. `DuplicateReviewException` and the
`409` mapping are removed. Editing a specific past entry still uses
`PUT …/{id}` with the existing ownership check; deleting an entry uses
`DELETE …/{id}` (owner or admin).

## 5. Average and count semantics

`AverageRating` = mean of all entries' ratings for the coffee (all users, all
dates); `ReviewCount` = number of entries. A user rating the same coffee three
times therefore weights it 3× — intentional: a coffee rated often and highly over
time is genuinely well-liked. Documented so the UI can label it "N ratings" rather
than "N reviewers".

## 6. Migration safety

Single-instance SQLite, migrate-on-startup. Dropping a unique index, adding a
non-unique index, and adding a nullable column are non-destructive; existing rows
keep their data and become each user's first entry. SQLite performs index/column
changes by table rebuild under EF — verify the generated migration applies cleanly
against a copy of a populated DB before relying on it.
