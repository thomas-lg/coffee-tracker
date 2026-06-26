## 1. DTOs & ports (Application)

- [x] 1.1 Add `Dtos/CoffeeCreateDto.cs` with DataAnnotations (`[Required]` strings, non-negative `Price`, optional `[Url]` `PurchaseUrl`)
- [x] 1.2 Add `Dtos/CoffeeUpdateDto.cs` (same shape as create; id comes from the route)
- [x] 1.3 Extend `Ports/Driven/ICoffeeRepository.cs` with `GetByIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`
- [x] 1.4 Extend `Ports/Driving/ICoffeeCatalogService.cs` with `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `SetPhotoAsync`
- [x] 1.5 Add `Ports/Driven/IPhotoStorage.cs` (save validated upload → relative path; typed rejection for bad type/size)

## 2. Application service

- [x] 2.1 Implement `CreateAsync` (map DTO→entity, stamp `CreatedAt`, persist, return response DTO)
- [x] 2.2 Implement `GetByIdAsync` / `UpdateAsync` / `DeleteAsync` with not-found handling (no exceptions for control flow)
- [x] 2.3 Implement `SetPhotoAsync` (store via `IPhotoStorage`, set `PhotoPath`, persist)

## 3. Infrastructure adapters

- [x] 3.1 Extend `EfCoffeeRepository` with `GetByIdAsync`/`AddAsync`/`UpdateAsync`/`DeleteAsync` (`SaveChangesAsync`)
- [x] 3.2 Add `Storage/FileSystemPhotoStorage.cs`: content-type allowlist (jpeg/png/webp), size cap, `Guid` filename, relative path return
- [x] 3.3 Bind `Storage:PhotosPath` (default `photos`) + `Storage:MaxPhotoBytes` (default 5 MB) from config; register `IPhotoStorage` in `AddInfrastructure`

## 4. API endpoints & wiring

- [x] 4.1 Add to `CoffeesController`: `GET {id}` (404 if missing), `POST` (201 + Location), `PUT {id}` (404/204), `DELETE {id}` (404/204)
- [x] 4.2 Add `POST /api/coffees/{id}/photo` (multipart `IFormFile`): 404 if coffee missing, 400 on invalid upload, 200 with updated DTO on success
- [x] 4.3 Serve the photos directory read-only at `/photos` via static-file middleware in `Program.cs`
- [x] 4.4 Add `Storage` config keys to `appsettings.json`; git-ignore the local `photos/` directory

## 5. Verify

- [x] 5.1 Unit tests: service CRUD + photo set against fake repository/storage (in-memory), incl. not-found and invalid-upload paths
- [x] 5.2 `dotnet build CoffeeTracker.sln` succeeds with no new warnings
- [x] 5.3 `dotnet test` passes (13 tests)
- [x] 5.4 Confirm `dotnet ef migrations add` would be empty (no schema drift) — verified via `migrations has-pending-model-changes`
- [x] 5.5 Manual smoke in Swagger/curl: create → get → update → upload photo (loaded back via `/photos/...`, served as `image/png`) → delete; oversized/wrong-type upload rejected; directory listing on `/photos` returns 404
