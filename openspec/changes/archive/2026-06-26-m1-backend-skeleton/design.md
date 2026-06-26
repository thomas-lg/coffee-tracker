## Context

The repo has no backend yet. M1 stands up `CoffeeTracker.Api` (.NET 10, controller-based) with EF Core + SQLite and one read endpoint. The stack is fixed by `PLAN.md`: .NET 10, EF Core code-first, file-based SQLite, Swagger in dev, HTTP-only `localhost:5000` (dev is a secure context for later PWA/camera work). Commands are intended to run inside the M0 dev container; the host also has the .NET 10 SDK.

## Goals / Non-Goals

**Goals:**
- A runnable API with `GET /api/coffees` backed by a migrated SQLite database.
- Establish the EF Core code-first → migrations → DbContext-via-DI pattern for later milestones.
- DTO boundary in place from the start so the entity is never serialized directly.

**Non-Goals:**
- Write operations / full CRUD (M2), photo upload (M2), auth (M3), reviews (M4), OCR (M5).
- Production hosting, HTTPS, forwarded headers, startup auto-migration (M7).

## Decisions

- **SQLite, file-based (`Data Source=coffee.db`)** over in-memory: matches production single-file persistence and exercises real migrations. Connection string lives in `appsettings.json`, bound via `AddDbContext<AppDbContext>(o => o.UseSqlite(config.GetConnectionString(...)))`.
- **Controllers (`--use-controllers`)** over minimal APIs: `PLAN.md` calls for controllers; scales better to the CRUD/auth surface of later milestones.
- **Response DTO (`CoffeeResponseDto` record) introduced now** even though only a read endpoint exists, so M2's create/update DTOs extend an existing `Dtos/` folder rather than retrofitting a boundary later.
- **Migrations are committed; `coffee.db` is git-ignored** — migrations are versioned source of truth for the schema; the DB file is regenerable local state.
- **EF design-time tooling**: `dotnet ef` must be ≥ the EF Core 10 runtime packages. If the installed global tool is older (e.g. 9.x), update it (`dotnet tool update --global dotnet-ef --version 10.*`) before running migrations.

## Risks / Trade-offs

- [`dotnet ef` version older than EF Core 10 runtime → migration/design commands fail] → update the global `dotnet-ef` tool to a 10.x version before `migrations add`.
- [Host vs dev-container drift] → prefer running inside the dev container per `PLAN.md`; the host SDK is a convenience fallback and produces identical migration output.
- [Serializing the entity by accident] → the controller projects to `CoffeeResponseDto`; the entity type is never returned from an action.
