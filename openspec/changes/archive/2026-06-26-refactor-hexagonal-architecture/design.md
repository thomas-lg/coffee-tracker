## Context

M1's API is a single project where the controller talks to EF Core directly. The team wants a hexagonal (ports & adapters) architecture with the boundary enforced structurally (by project references), not just by convention. The product surface is tiny (one entity, one read endpoint), so this is the cheapest possible moment to lay the layering down.

## Goals / Non-Goals

**Goals:**
- The HTTP layer (controllers) depends only on an application-layer port, never on EF Core / `DbContext`.
- The dependency rule is compiler-enforced: Domain depends on nothing; Application depends only on Domain; Infrastructure and Api depend inward.
- Persistence is a swappable driven adapter behind `ICoffeeRepository`.
- Identical runtime behavior for `GET /api/coffees`.

**Non-Goals:**
- New product behavior, CRUD, auth, or any milestone beyond M1.
- A separate bootstrapper project to keep Api from referencing Infrastructure (Api references Infrastructure only at the composition root — acceptable for this size).
- CQRS/MediatR (considered and rejected as heavier than needed here).

## Decisions

- **Multi-project hexagonal (Domain / Application / Infrastructure / Api)** over single-project layering: the user explicitly wants the boundary enforced; project references make "controller references DbContext" a compile error, which convention cannot guarantee.
- **Ports live in Application.** Driving port `ICoffeeCatalogService` (input, implemented by `CoffeeCatalogService`); driven port `ICoffeeRepository` (output, implemented in Infrastructure by `EfCoffeeRepository`). Domain holds only the `Coffee` entity and stays framework-free.
- **No generic repository.** A focused `ICoffeeRepository` (not `IRepository<T>`) — purpose-built ports, not a leaky generic abstraction over EF.
- **DTO mapping in the Application service.** `EfCoffeeRepository` returns domain `Coffee`; `CoffeeCatalogService` maps to `CoffeeResponseDto`. The controller returns the DTO unchanged.
- **Composition root in Api.** `AddApplication()` and `AddInfrastructure(IConfiguration)` extension methods register the wiring; `Program.cs` calls both. Api references Infrastructure solely to call `AddInfrastructure`.
- **Migrations relocate to Infrastructure** (where `AppDbContext` lives). Design-time: `dotnet ef ... -p backend/CoffeeTracker.Infrastructure -s backend/CoffeeTracker.Api`.
- **Keep the SQLite ordering fix** (`OrderByDescending(c => c.Id)`) inside `EfCoffeeRepository` — SQLite can't `ORDER BY` a `DateTimeOffset`.

## Risks / Trade-offs

- [Api still references Infrastructure at the composition root, a slight purity compromise] → contained to `Program.cs` + `AddInfrastructure`; controllers/services never see it. A bootstrapper project could remove it but isn't worth the ceremony here.
- [EF design-time commands now need explicit `-p`/`-s`] → documented in tasks and `PLAN.md`; the regenerated `InitialCreate` must produce the same schema as M1's.
- [More projects to navigate for a small app] → accepted; the team prioritizes the enforced boundary and this scales into later milestones.
