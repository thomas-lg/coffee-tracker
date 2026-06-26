## Why

The app now has authenticated users (M3) and a coffee catalog (M2), but no way to capture the actual point of the app: what each user **thinks** of a coffee. Milestone M4 in `PLAN.md` adds per-user reviews (rating + tasting notes + brew details), a shared set of flavor tags, and surfaces each coffee's average rating — turning the catalog into a shared tasting log. It also lands the **ownership-based authorization** deferred from M3 (you may only edit/delete your own review).

## What Changes

- Add domain entities `Review` (`Rating` 1–5, `TastingNotes`, `BrewMethod`, `Grind`, `Ratio`, `CreatedAt`, `UpdatedAt`, owner `UserId`, `CoffeeId`) and `FlavorTag` (`Name`), with a many-to-many `Review`↔`FlavorTag` relationship.
- **One review per user per coffee**, enforced by a unique index on `(CoffeeId, UserId)` plus a pre-check that returns a typed "already reviewed" result.
- New `reviews` capability behind a driving port `IReviewService`, exposed under the coffee:
  - `GET /api/coffees/{coffeeId}/reviews` — list a coffee's reviews
  - `POST /api/coffees/{coffeeId}/reviews` — create *my* review
  - `PUT /api/coffees/{coffeeId}/reviews/{id}` — update *my* review (ownership-checked)
  - `DELETE /api/coffees/{coffeeId}/reviews/{id}` — delete *my* review (ownership-checked)
  - `GET /api/flavor-tags` — list the available flavor tags
- Seed a starter flavor-tag set (fruity, chocolatey, nutty, floral, fruity, caramel, …) via EF `HasData` so it ships with the schema.
- Extend `CoffeeResponseDto` (list + get-one) with `AverageRating` (nullable) and `ReviewCount`, computed with an aggregate query (no N+1).
- All review/tag endpoints require authentication (consistent with M3's "account mandatory").

Out of scope (deferred): admin moderation of others' reviews (owner-only for now); editing the flavor-tag set via API (tags are seed-managed); review photos; pagination/sorting of reviews (small per-coffee volume); helpfulness votes.

## Capabilities

### New Capabilities
- `reviews`: per-user coffee reviews (rating, notes, brew details, flavor tags) with one-per-user-per-coffee and owner-only edit/delete.

### Modified Capabilities
- `coffee-catalog`: coffee responses gain `AverageRating` and `ReviewCount`.

## Impact

- **Modified projects:** `CoffeeTracker.Domain` (`Review`, `FlavorTag`), `CoffeeTracker.Application` (review DTOs + `IReviewService` driving port + `IReviewRepository`/`IFlavorTagRepository` driven ports; `CoffeeResponseDto` gains two fields; catalog service maps them), `CoffeeTracker.Infrastructure` (EF config: relationships, unique index, `HasData`; repositories; migration), `CoffeeTracker.Api` (`ReviewsController`, `FlavorTagsController`).
- **New EF migration:** adds `Reviews`, `FlavorTags`, the join table, the unique index, and the seeded tags. Applies on startup.
- **Behaviour change:** `CoffeeResponseDto` JSON gains `averageRating`/`reviewCount` (additive).
