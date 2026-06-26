# Design notes — M2 Coffee CRUD + photo upload

## Where validation and mapping live (hexagonal layering)

- **DTO shape + field-level validation** (`[Required]`, ranges, `[Url]`) lives on the
  DTOs in the Application layer and is enforced by the ASP.NET Core model-binding
  pipeline (`[ApiController]` auto-400). Controllers stay thin: bind → call the
  driving port → translate the result to an HTTP status.
- **DTO ↔ domain mapping** stays in `CoffeeCatalogService` (Application). The domain
  `Coffee` entity and the EF adapter never see DTOs; the controller never sees the
  entity. This preserves the M1 rule "controllers depend only on application ports".
- **Not-found** is represented as a `null`/`bool` return from the service (no
  exceptions for control flow); the controller maps that to `404`.

## Photo storage as a driven port

`IPhotoStorage` is an output port (like `ICoffeeRepository`) so the application
service can attach photos without knowing about the filesystem. The adapter
(`FileSystemPhotoStorage`) owns all the security-relevant decisions, because they
are storage concerns:

- **Content-type allowlist:** `image/jpeg`, `image/png`, `image/webp`. The
  extension is derived from the *allowlisted content type*, never from the
  user-supplied filename.
- **Size cap:** `Storage:MaxPhotoBytes` (default 5 MB), enforced before/while
  streaming to disk.
- **Random filenames:** `Guid.NewGuid()` + derived extension. Because the filename
  is server-generated and the directory is fixed, there is no user-controlled path
  segment, so directory-traversal (`../`) is structurally impossible rather than
  filtered.
- **Return value:** a *relative* path (e.g. `photos/{guid}.jpg`) stored in
  `Coffee.PhotoPath`, so the stored value is independent of the absolute mount
  point and survives a container/volume remap.

Rejected input (wrong type / too large) surfaces as a typed result the service
turns into a `400`, not a 500.

## Serving photos

Stored images are served by the static-file middleware bound to the photos
directory at request path `/photos`, **read-only** (no directory browsing, no
execution). This keeps image delivery off the controller/EF path. A streaming
controller endpoint was considered but is unnecessary for M2 and would add an
auth surface we don't need yet (photos are not secret in this app).

## No new migration

The `Coffee` table created by `InitialCreate` already has `PhotoPath`, `ShopName`,
`PurchaseUrl`, `CreatedByUserId`, and `CreatedAt`. M2 only writes columns that
already exist, so there is no schema change and no new migration — verified by
running `dotnet ef migrations add` would produce an empty diff.

## Service surface decision

We extend the existing `ICoffeeCatalogService` rather than introduce a separate
write service. "Catalog" reasonably covers managing the catalog, the surface is
small (5 methods), and one port keeps the controller's dependency list minimal.
If the read and write concerns diverge later (e.g. CQRS, separate authz), the
port can be split then without churning callers.
