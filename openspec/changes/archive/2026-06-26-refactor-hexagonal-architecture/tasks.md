## 1. Create projects & wire the dependency rule

- [x] 1.1 `dotnet new classlib` for `CoffeeTracker.Domain`, `CoffeeTracker.Application`, `CoffeeTracker.Infrastructure` under `backend/`
- [x] 1.2 Add all three projects to `CoffeeTracker.sln`
- [x] 1.3 Set project references: Application→Domain; Infrastructure→Application(+Domain); Api→Application (and Infrastructure for composition root only)
- [x] 1.4 Move EF packages (`Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`, pinned `SQLitePCLRaw.bundle_e_sqlite3` 3.0.3) from Api to Infrastructure

## 2. Move types to their layers

- [x] 2.1 Move `Coffee` entity → `CoffeeTracker.Domain` (no framework deps)
- [x] 2.2 Move `CoffeeResponseDto` → `CoffeeTracker.Application`
- [x] 2.3 Move `AppDbContext` → `CoffeeTracker.Infrastructure/Persistence`
- [x] 2.4 Delete now-empty `Api/Models`, `Api/Data`, `Api/Dtos`

## 3. Ports & adapters

- [x] 3.1 Define driven port `ICoffeeRepository` (Application)
- [x] 3.2 Define driving port `ICoffeeCatalogService` (Application)
- [x] 3.3 Implement `CoffeeCatalogService` (Application) — uses repository, maps domain→DTO
- [x] 3.4 Implement `EfCoffeeRepository` (Infrastructure) — EF query, `OrderByDescending(c => c.Id)`
- [x] 3.5 Add `AddApplication()` (Application) and `AddInfrastructure(IConfiguration)` (Infrastructure) DI extensions

## 4. Rewire Api

- [x] 4.1 Rewrite `CoffeesController` to depend on `ICoffeeCatalogService` only
- [x] 4.2 `Program.cs` calls `AddApplication()` + `AddInfrastructure(builder.Configuration)`; remove all EF/DbContext usage from Api
- [x] 4.3 Keep connection string in Api `appsettings.json`; `AddInfrastructure` reads it

## 5. Migrations

- [x] 5.1 Remove old `Api/Migrations`
- [x] 5.2 `dotnet ef migrations add InitialCreate -p backend/CoffeeTracker.Infrastructure -s backend/CoffeeTracker.Api`
- [x] 5.3 `dotnet ef database update -s backend/CoffeeTracker.Api`; confirm same schema

## 6. Tests & docs

- [x] 6.1 Update `CoffeeTracker.Tests` references (Application/Domain; Api for integration)
- [x] 6.2 Add a `CoffeeCatalogService` unit test using a fake `ICoffeeRepository` (proves the boundary)
- [x] 6.3 Update `PLAN.md` "Target repo structure" + milestone notes for the hexagonal layout

## 7. Verify

- [x] 7.1 `grep -r DbContext backend/CoffeeTracker.Api` → no controller hits; Domain `.csproj` has no EF/framework refs
- [x] 7.2 `dotnet build CoffeeTracker.sln` → 0 warnings / 0 errors
- [x] 7.3 `dotnet test` passes (SanityTests + new service test)
- [x] 7.4 Run API; `GET /api/coffees` → `200 []`, and a DTO after inserting a row; Swagger UI loads
