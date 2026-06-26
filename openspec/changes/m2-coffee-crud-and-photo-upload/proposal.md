## Why

M1 left the catalog read-only (`GET /api/coffees`). Milestone M2 in `PLAN.md` makes the catalog *editable*: clients can create, update, and delete coffees, and attach a photo of the bag. This completes the core data-management loop the rest of the app (reviews, OCR snap-to-fill, the PWA) builds on, and exercises the ASP.NET Core model-binding/validation pipeline and `IFormFile` upload handling.

## What Changes

- Add `CoffeeCreateDto` and `CoffeeUpdateDto` records with DataAnnotations validation (required fields, non-negative price, well-formed URL).
- Extend the driven repository port (`ICoffeeRepository`) with `GetByIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`; implement them in the EF Core adapter.
- Extend the driving port (`ICoffeeCatalogService`) with get-one / create / update / delete operations and a set-photo operation; the application service maps DTO ↔ domain entity and stamps `CreatedAt`.
- Add full CRUD to `CoffeesController`: `GET /api/coffees/{id}`, `POST /api/coffees`, `PUT /api/coffees/{id}`, `DELETE /api/coffees/{id}` — all async, returning correct status codes (201 + Location on create, 404 on missing, 204 on delete).
- Add a `photo-storage` capability: a driven `IPhotoStorage` port and a filesystem adapter that saves uploads to a configurable directory with a **content-type allowlist, size cap, and server-generated random filenames** (no user-controlled path → path-traversal-safe).
- Add `POST /api/coffees/{id}/photo` (multipart `IFormFile`) → validate, store, set `PhotoPath`; serve stored images read-only under `/photos`.

Out of scope (later milestones): authentication / ownership (`CreatedByUserId` stays null until M3), reviews & ratings (M4), OCR (M5).

## Capabilities

### New Capabilities
- `photo-storage`: validating and persisting uploaded coffee-bag photos to disk and serving them back over HTTP.

### Modified Capabilities
- `coffee-catalog`: gains create / read-one / update / delete operations and the ability to associate a stored photo with a coffee.

## Impact

- **Modified projects:** `CoffeeTracker.Application` (DTOs, ports, service), `CoffeeTracker.Infrastructure` (EF repo, photo-storage adapter, DI), `CoffeeTracker.Api` (controller, static-file wiring), `CoffeeTracker.Tests`.
- **No new EF migration:** the `Coffee` table already has every column M2 writes to (`PhotoPath` was reserved in M1). No schema change.
- **Configuration:** new `Storage:PhotosPath` (default `photos`) and `Storage:MaxPhotoBytes` (default 5 MB) keys in `appsettings.json`; the `photos/` directory and its contents are git-ignored.
- **Runtime artifact:** uploaded image files under the configured photos directory (mapped to the `/photos` container volume in M7).
