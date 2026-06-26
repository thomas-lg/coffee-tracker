## 1. Domain & persistence

- [ ] 1.1 Add `Review` entity (Domain): Id, CoffeeId, UserId, Rating, TastingNotes, BrewMethod?, Grind?, Ratio?, CreatedAt, UpdatedAt, `ICollection<FlavorTag> Tags`
- [ ] 1.2 Add `FlavorTag` entity (Domain): Id, Name, `ICollection<Review> Reviews`
- [ ] 1.3 Add `DbSet<Review>` + `DbSet<FlavorTag>` to `AppDbContext`; configure 1-many Coffee→Review, M2M Review↔FlavorTag, unique index on (CoffeeId, UserId)
- [ ] 1.4 Seed starter flavor tags via `HasData`
- [ ] 1.5 Generate `AddReviews` migration; confirm it applies on a fresh DB

## 2. Ports & DTOs (Application)

- [ ] 2.1 DTOs: `ReviewCreateDto`/`ReviewUpdateDto` (Rating [Range 1–5], notes, brew fields, tag ids) + `ReviewResponseDto` (incl. tags, owner id, timestamps) + `FlavorTagDto`
- [ ] 2.2 Driven ports: `IReviewRepository` (list by coffee, get-by-coffee-and-user, get-by-id, add, update, delete, rating aggregate) + `IFlavorTagRepository` (list, get-by-ids)
- [ ] 2.3 Driving port `IReviewService` (+ typed result: Success/CoffeeNotFound/AlreadyReviewed/NotFound/NotOwner) and `IFlavorTagService` (or reuse review service for tag listing)
- [ ] 2.4 Add `AverageRating`/`ReviewCount` to `CoffeeResponseDto`

## 3. Application service

- [ ] 3.1 List reviews for a coffee (404 if coffee missing)
- [ ] 3.2 Create my review: 404 if coffee missing, 409 if I already reviewed, resolve tag ids, stamp owner + CreatedAt
- [ ] 3.3 Update my review: 404 if missing, 403 if not mine; replace fields + tags, stamp UpdatedAt
- [ ] 3.4 Delete my review: 404 if missing, 403 if not mine
- [ ] 3.5 List flavor tags

## 4. Infrastructure adapters

- [ ] 4.1 `EfReviewRepository` (eager-load Tags; aggregate avg/count) + `EfFlavorTagRepository`
- [ ] 4.2 Coffee repository: compute `AverageRating`/`ReviewCount` via a group-by projection (no N+1) in list + get-one
- [ ] 4.3 Register repositories/services in `AddInfrastructure`/`AddApplication`

## 5. API

- [ ] 5.1 `ReviewsController` ([Authorize]): GET list, POST create, PUT update, DELETE — map result status → 200/201/403/404/409
- [ ] 5.2 `FlavorTagsController` ([Authorize]): GET list

## 6. Verify

- [ ] 6.1 Unit tests (service against fakes): create stamps owner + tags; second review by same user → AlreadyReviewed; update/delete by non-owner → NotOwner; average/count mapping
- [ ] 6.2 `dotnet build` clean; `dotnet test` green
- [ ] 6.3 Manual smoke: two users review one coffee; average + per-user reviews correct; editing another user's review → 403; tag list returns seeds; coffee response shows averageRating/reviewCount
