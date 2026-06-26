## Why

The coffee-tracker repo is scaffolding only (dev container, CI/CD, test skeleton) with no application code. Milestone M1 in `PLAN.md` establishes the backend foundation — a runnable .NET 10 Web API with a real SQLite database and one read endpoint — so every later milestone (CRUD, auth, reviews, OCR) has a working API + EF Core code-first migration loop to build on.

## What Changes

- Introduce the `CoffeeTracker.Api` (.NET 10, controllers) project and add it to the solution; wire the test project's `ProjectReference` to it.
- Add a `Coffee` domain entity and an EF Core `AppDbContext` backed by file-based SQLite (`coffee.db`).
- Add an initial EF Core migration (`InitialCreate`) that creates the coffee table.
- Expose `GET /api/coffees` returning a list of coffees as a response DTO (no entity leakage).
- Wire Swagger/OpenAPI and an HTTP-only `http://localhost:5000` dev profile.

Out of scope (later milestones): write endpoints / full CRUD, photo upload (M2), authentication (M3), reviews & tags (M4), OCR (M5).

## Capabilities

### New Capabilities
- `coffee-catalog`: persisting coffee records in a database and reading the catalog over an HTTP API.

### Modified Capabilities
<!-- None — this is the first capability. -->

## Impact

- **New project:** `backend/CoffeeTracker.Api` (Program.cs, controllers, models, data, DTOs, migrations).
- **Dependencies:** `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`, `Swashbuckle.AspNetCore`.
- **Modified files:** `CoffeeTracker.sln` (add API project), `backend/CoffeeTracker.Tests/CoffeeTracker.Tests.csproj` (enable ProjectReference), `.gitignore` (ignore local `coffee.db`).
- **Runtime artifact:** local `coffee.db` SQLite file (not committed; migrations are committed).
