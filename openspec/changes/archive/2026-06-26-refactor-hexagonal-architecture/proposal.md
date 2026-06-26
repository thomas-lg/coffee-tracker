## Why

M1 shipped with `CoffeesController` querying `AppDbContext` directly — the HTTP layer is coupled to the persistence mechanism, which the team considers a non-starter. Establishing a hexagonal (ports & adapters) boundary now, while the surface is one entity and one endpoint, keeps every later milestone (CRUD, auth, reviews, OCR) on the right side of that boundary instead of retrofitting it later.

## What Changes

- Split the single `CoffeeTracker.Api` project into a **multi-project hexagonal** layout: `CoffeeTracker.Domain`, `CoffeeTracker.Application`, `CoffeeTracker.Infrastructure`, `CoffeeTracker.Api`.
- Move the `Coffee` entity to Domain, `CoffeeResponseDto` to Application, `AppDbContext` to Infrastructure.
- Introduce ports: a driving port (`ICoffeeCatalogService`, in Application) and a driven port (`ICoffeeRepository`, in Application), with an EF Core driven adapter (`EfCoffeeRepository`, in Infrastructure).
- `CoffeesController` depends only on the driving port; the EF migration moves to Infrastructure.
- No change to HTTP behavior — `GET /api/coffees` returns the same payload. **Not a breaking change** for API clients.

## Capabilities

### New Capabilities
<!-- None — no new product capability. -->

### Modified Capabilities
- `coffee-catalog`: adds an architectural requirement that the HTTP/API layer must not depend directly on the persistence mechanism (data access goes through a port). Behavior of existing requirements is unchanged.

## Impact

- **New projects:** `backend/CoffeeTracker.Domain`, `backend/CoffeeTracker.Application`, `backend/CoffeeTracker.Infrastructure`.
- **Modified:** `backend/CoffeeTracker.Api` (controllers + composition root only), `CoffeeTracker.sln`, `backend/CoffeeTracker.Tests` (project references), `PLAN.md` (repo-structure section), EF migration relocated to Infrastructure.
- **No dependency changes** beyond moving the existing EF/SQLite packages from Api to Infrastructure.
- **Risk:** EF design-time commands now need `-p Infrastructure -s Api`; `coffee.db` is regenerated from the relocated migration.
