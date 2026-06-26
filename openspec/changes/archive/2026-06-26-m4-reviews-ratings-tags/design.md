# Design notes — M4 Reviews, ratings & flavor tags

Decisions worth reviewing before implementation; defaults noted — flag any to change.

## 1. Entities & relationships (hexagon-clean)

`Review` and `FlavorTag` are plain domain entities (Domain project, framework-free).
`Review` holds the owner as a `UserId` **string** (same as `Coffee.CreatedByUserId`)
— the domain never references the Identity `AppUser` type. Navigation:
`Review.Tags : ICollection<FlavorTag>` and `FlavorTag.Reviews : ICollection<Review>`.

**Many-to-many:** EF Core implicit join (skip navigations) — no explicit `ReviewTag`
entity in the domain; EF creates the join table automatically. Less boilerplate than
a hand-written join entity, and we don't need payload columns on the join.

## 2. One review per user per coffee

Enforced two ways: a **unique index on `(CoffeeId, UserId)`** (the source of truth),
*and* a service-level pre-check (`GetByCoffeeAndUserAsync`) that returns a typed
`AlreadyReviewed` result → `409`, so the normal path doesn't rely on catching a DB
constraint violation. The index is the backstop against a race.

## 3. REST shape — reviews nested under the coffee

Reviews are a sub-resource of a coffee, so they live under
`/api/coffees/{coffeeId}/reviews`. `POST` creates *your* review (409 if you already
have one for this coffee); `PUT .../{id}` and `DELETE .../{id}` operate on a specific
review with an **ownership check**. Flavor tags are global, so `GET /api/flavor-tags`
is top-level.

## 4. Ownership authorization (the M3 deferral)

Editing requires `review.UserId == currentUser.Id`; otherwise `403` (owner-only).
**Deleting is allowed for the owner OR an admin** (moderation) — admins cannot *edit*
others' reviews, only delete them. This needs the caller's admin status, so
`ICurrentUser` gains an `IsAdmin` flag (read from the `isAdmin` JWT claim). This is
the ownership authorization deferred from M3.

## 5. Average rating & review count on `CoffeeResponseDto`

`CoffeeResponseDto` gains `AverageRating` (nullable `double` — null when no reviews)
and `ReviewCount` (`int`). **Computed with a single aggregate/group-by projection**
in the coffee repository, not per-row — no N+1. The list endpoint stays one query.
(Alternative considered: a separate `GET /api/coffees/{id}` detail endpoint carrying
the aggregates — rejected for now since the list view wants the average too.)

## 6. Flavor-tag seeding

Starter tags (fruity, chocolatey, nutty, floral, citrus, berry, caramel, spicy,
earthy, floral…) are seeded via EF **`HasData`** in `OnModelCreating`, so they ship
inside the migration and apply idempotently — no runtime `DbSeeder` to remember to
run. Tags have fixed ids in the seed. (PLAN mentions a `DbSeeder`; `HasData` achieves
the same outcome more simply and is migration-tracked.)

## 7. Rating validation

`Rating` is validated `1–5` via DataAnnotations on the create/update DTOs (the
model-binding pipeline → 400), mirroring the M2 DTO approach. `TastingNotes` is
length-bounded; `BrewMethod`/`Grind`/`Ratio` are optional free-text.

## 8. Auth

`ReviewsController` and `FlavorTagsController` are `[Authorize]` (all endpoints need a
token, consistent with M3). Create/update/delete read the owner id from the
`ICurrentUser` port (introduced in M3).

## 9. Timestamps

`CreatedAt`/`UpdatedAt` stamped via the injected `TimeProvider` (as in M2/M3), so
they stay testable.
