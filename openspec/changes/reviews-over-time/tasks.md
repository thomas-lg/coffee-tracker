## 1. Domain & DTOs

- [x] 1.1 Add optional `Stage` (nullable string) to `Review` (Domain)
- [x] 1.2 Add optional `Stage` (max 40) to `ReviewCreateDto`/`ReviewUpdateDto` and `ReviewResponseDto`

## 2. Persistence (Infrastructure)

- [x] 2.1 EF config: replace the UNIQUE index on `(CoffeeId, UserId)` with a non-unique index; map the `Stage` column
- [x] 2.2 Add migration `ReviewsOverTime`: drop `IX_Reviews_CoffeeId_UserId` (unique), add non-unique index, add nullable `Stage`
- [x] 2.3 Order `GET …/reviews` newest-first in `EfReviewRepository` (by `Id` desc — SQLite can't ORDER BY `DateTimeOffset`; `Id` tracks creation order)

## 3. Application / API behavior

- [x] 3.1 `ReviewService`: remove duplicate rejection; `POST` always creates a dated entry; persist `Stage`
- [x] 3.2 Remove `DuplicateReviewException`; drop the `409` mapping and `AlreadyReviewed` status
- [x] 3.3 Re-verify `AverageRating`/`ReviewCount` across all entries (mean of all ratings; count = entries) — confirmed 4.5 / 2 in smoke test

## 4. Tests

- [x] 4.1 Invert one-per-user tests → assert multiple dated entries are allowed; add a `Stage` round-trip test
- [x] 4.2 Average/count correct across multiple entries

## 5. Verify

- [x] 5.1 `dotnet build` clean; `dotnet test` green (80 passed)
- [x] 5.2 Migration applies on the existing populated DB with no data loss (applied on startup)
- [x] 5.3 Manual: `POST` the same coffee twice → two entries (`201`/`201`); `GET` returns them newest-first
