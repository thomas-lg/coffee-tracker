## 1. Ports (Application)

- [x] 1.1 Add `Task<IReadOnlyList<string>> ListAsync(CancellationToken)` to `IPhotoStorage`
- [x] 1.2 Add `Task<IReadOnlyList<string>> GetUsedPhotoPathsAsync(CancellationToken)` to `ICoffeeRepository`
- [x] 1.3 New driving port `IPhotoAdminService`: `ListAsync` (paths + used flag) and `DeleteAsync(paths)` → result (deleted/skipped counts)

## 2. Adapters (Infrastructure)

- [x] 2.1 `FileSystemPhotoStorage.ListAsync`: enumerate files in the photos dir → `photos/{name}`; empty when the dir is absent
- [x] 2.2 `EfCoffeeRepository.GetUsedPhotoPathsAsync`: project non-null `PhotoPath` values

## 3. Service (Application)

- [x] 3.1 `PhotoAdminService.ListAsync`: stored ∖ used → mark each used/unused
- [x] 3.2 `PhotoAdminService.DeleteAsync`: delete only requested paths that are still unused; skip used; report counts

## 4. API

- [x] 4.1 DTOs: `PhotoListItemDto(Path, Used)`, `PhotoDeleteRequestDto(Paths)`, `PhotoDeleteResultDto(Deleted, Skipped)`
- [x] 4.2 `Admin` authorization policy in `Program.cs` over the `isAdmin` claim
- [x] 4.3 `AdminPhotosController` (`[Authorize(Policy = "Admin")]`): `GET` + `DELETE /api/admin/photos`
- [x] 4.4 Register `IPhotoAdminService` in `AddApplication`

## 5. Tests

- [x] 5.1 Service: orphan listed as unused; coffee-referenced path listed as used
- [x] 5.2 Service: delete removes unused; a now-used path in the request is skipped, not deleted
- [ ] 5.3 Authorization: non-admin gets `403`, admin succeeds — enforced declaratively by the
  `Admin` policy; no automated test (the suite has no HTTP integration harness yet). Verify manually.

## 6. Verify

- [x] 6.1 `dotnet build` + `dotnet test` green (83 tests)
