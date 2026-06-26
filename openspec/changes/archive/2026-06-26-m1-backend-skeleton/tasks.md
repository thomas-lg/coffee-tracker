## 1. Project setup

- [x] 1.1 Create `CoffeeTracker.Api` (.NET 10, `--use-controllers`) at `backend/CoffeeTracker.Api`
- [x] 1.2 Add the API project to `CoffeeTracker.sln`
- [x] 1.3 Enable the `ProjectReference` to the API in `backend/CoffeeTracker.Tests/CoffeeTracker.Tests.csproj`
- [x] 1.4 Add packages: `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`, `Swashbuckle.AspNetCore`
- [x] 1.5 Ensure `dotnet-ef` tooling is EF Core 10.x compatible (update global tool if needed)

## 2. Data model & persistence

- [x] 2.1 Add `Models/Coffee.cs` with Id, Name, Roaster, Origin, RoastLevel, Price, DateBought, PhotoPath?, ShopName?, PurchaseUrl?, CreatedByUserId, CreatedAt
- [x] 2.2 Add `Data/AppDbContext.cs` with `DbSet<Coffee> Coffees`
- [x] 2.3 Add `Data Source=coffee.db` connection string to `appsettings.json`
- [x] 2.4 Register `AppDbContext` with SQLite in `Program.cs`
- [x] 2.5 Create `InitialCreate` migration and apply it (`dotnet ef database update`)
- [x] 2.6 Git-ignore local `coffee.db`; keep `Migrations/` committed

## 3. Read endpoint & API wiring

- [x] 3.1 Add `Dtos/CoffeeResponseDto.cs` record
- [x] 3.2 Add `Controllers/CoffeesController.cs` with async `GET /api/coffees` projecting entities to DTOs
- [x] 3.3 Wire controllers + Swagger/OpenAPI (UI in Development) in `Program.cs`
- [x] 3.4 Configure HTTP-only `http://localhost:5000` dev profile in `Properties/launchSettings.json`

## 4. Verify

- [x] 4.1 `dotnet build CoffeeTracker.sln` succeeds
- [x] 4.2 `dotnet test` passes (existing `SanityTests`)
- [x] 4.3 Run the API; `GET /api/coffees` returns HTTP 200 + empty JSON array; `coffee.db` is created
- [x] 4.4 Swagger UI lists and can invoke `GET /api/coffees` in Development
