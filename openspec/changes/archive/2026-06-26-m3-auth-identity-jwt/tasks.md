## 1. Identity data model & persistence

- [x] 1.1 Add packages: `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (Infrastructure), `Microsoft.AspNetCore.Authentication.JwtBearer` (Api)
- [x] 1.2 Add `AppUser : IdentityUser` (`IsAdmin`, `DisplayName`) in Infrastructure
- [x] 1.3 Switch `AppDbContext` to `IdentityDbContext<AppUser>` (call `base.OnModelCreating`)
- [x] 1.4 Generate `AddIdentity` migration; confirm it applies on a fresh DB and the existing `Coffees` table is unchanged

## 2. Auth port, DTOs & adapter

- [x] 2.1 Add DTOs (Application): `RegisterDto`, `LoginDto` (DataAnnotations), `AuthResponseDto` (token + expiry + display name)
- [x] 2.2 Add driving port `IAuthService` + typed `AuthResult` (status enum + token) in Application
- [x] 2.3 Add `TokenService` (Infrastructure): issue HS256 JWT with subject = user id + `IsAdmin` claim; reads `Jwt:Key/Issuer/Audience`
- [x] 2.4 Add `IdentityAuthService` (Infrastructure) implementing `IAuthService` via `UserManager` only (JWT API, no cookies): register (gated by `REGISTRATION_ENABLED`, first user → admin), login (password check + manual lockout)
- [x] 2.5 Add `RegistrationOptions`/`JwtOptions` bound from config; register Identity + services in `AddInfrastructure`

## 3. API wiring & hardening

- [x] 3.1 `AuthController`: `POST /api/auth/register`, `POST /api/auth/login` → map `AuthResult` status to status codes
- [x] 3.2 Fail-fast startup check: throw if `Jwt:Key` missing or < 32 bytes
- [x] 3.3 `Program.cs`: `AddAuthentication().AddJwtBearer(...)` with validation params; ensure `UseAuthentication` precedes `UseAuthorization`
- [x] 3.4 Add rate limiter; apply a named policy to the auth endpoints (429 when tripped)
- [x] 3.5 Add `[Authorize]` at the `CoffeesController` level so all catalog endpoints (reads + writes) require a token; only auth endpoints stay anonymous

## 4. Owner stamping

- [x] 4.1 Add driven port `ICurrentUser { string? Id }` (Application); implement over `IHttpContextAccessor` in Api; register `AddHttpContextAccessor`
- [x] 4.2 `CoffeeCatalogService.CreateAsync` stamps `CreatedByUserId` from `ICurrentUser`

## 5. Config & docs

- [x] 5.1 Add `Jwt` section + `REGISTRATION_ENABLED` to `appsettings.json`; throwaway dev key in `appsettings.Development.json` (no real secret committed)
- [x] 5.2 README/PLAN: note auth endpoints + that the existing env vars (`Jwt__Key`, `REGISTRATION_ENABLED`) are now wired

## 6. Verify

- [x] 6.1 Unit tests: `IdentityAuthService`/owner-stamping against fakes — registration disabled → refused; first user is admin; bad credentials → invalid; weak password → rejected (where feasible without a real UserManager, otherwise cover `CoffeeCatalogService` owner stamping via `ICurrentUser` fake)
- [x] 6.2 `dotnet build` clean; `dotnet test` green
- [x] 6.3 App refuses to start with a missing/short `Jwt:Key`
- [x] 6.4 Manual smoke: flag off → register 403/blocked; flag on → first user admin, second user not; login returns a JWT; unauthenticated write → 401; authenticated write → 200 and `CreatedByUserId` set; repeated bad logins → 429/lockout
