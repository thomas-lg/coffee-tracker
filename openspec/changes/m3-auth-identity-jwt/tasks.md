## 1. Identity data model & persistence

- [ ] 1.1 Add packages: `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (Infrastructure), `Microsoft.AspNetCore.Authentication.JwtBearer` (Api)
- [ ] 1.2 Add `AppUser : IdentityUser` (`IsAdmin`, `DisplayName`) in Infrastructure
- [ ] 1.3 Switch `AppDbContext` to `IdentityDbContext<AppUser>` (call `base.OnModelCreating`)
- [ ] 1.4 Generate `AddIdentity` migration; confirm it applies on a fresh DB and the existing `Coffees` table is unchanged

## 2. Auth port, DTOs & adapter

- [ ] 2.1 Add DTOs (Application): `RegisterDto`, `LoginDto` (DataAnnotations), `AuthResponseDto` (token + expiry + display name)
- [ ] 2.2 Add driving port `IAuthService` + typed `AuthResult` (status enum + token) in Application
- [ ] 2.3 Add `TokenService` (Infrastructure): issue HS256 JWT with subject = user id + `IsAdmin` claim; reads `Jwt:Key/Issuer/Audience`
- [ ] 2.4 Add `IdentityAuthService` (Infrastructure) implementing `IAuthService` via `UserManager`/`SignInManager`: register (gated by `REGISTRATION_ENABLED`, first user → admin), login (password check + lockout)
- [ ] 2.5 Add `RegistrationOptions`/`JwtOptions` bound from config; register Identity + services in `AddInfrastructure`

## 3. API wiring & hardening

- [ ] 3.1 `AuthController`: `POST /api/auth/register`, `POST /api/auth/login` → map `AuthResult` status to status codes
- [ ] 3.2 Fail-fast startup check: throw if `Jwt:Key` missing or < 32 bytes
- [ ] 3.3 `Program.cs`: `AddAuthentication().AddJwtBearer(...)` with validation params; ensure `UseAuthentication` precedes `UseAuthorization`
- [ ] 3.4 Add rate limiter; apply a named policy to the auth endpoints (429 when tripped)
- [ ] 3.5 Add `[Authorize]` to catalog write actions (POST/PUT/DELETE + photo); leave GETs anonymous

## 4. Owner stamping

- [ ] 4.1 Add driven port `ICurrentUser { string? Id }` (Application); implement over `IHttpContextAccessor` in Api; register `AddHttpContextAccessor`
- [ ] 4.2 `CoffeeCatalogService.CreateAsync` stamps `CreatedByUserId` from `ICurrentUser`

## 5. Config & docs

- [ ] 5.1 Add `Jwt` section + `REGISTRATION_ENABLED` to `appsettings.json`; throwaway dev key in `appsettings.Development.json` (no real secret committed)
- [ ] 5.2 README/PLAN: note auth endpoints + that the existing env vars (`Jwt__Key`, `REGISTRATION_ENABLED`) are now wired

## 6. Verify

- [ ] 6.1 Unit tests: `IdentityAuthService`/owner-stamping against fakes — registration disabled → refused; first user is admin; bad credentials → invalid; weak password → rejected (where feasible without a real UserManager, otherwise cover `CoffeeCatalogService` owner stamping via `ICurrentUser` fake)
- [ ] 6.2 `dotnet build` clean; `dotnet test` green
- [ ] 6.3 App refuses to start with a missing/short `Jwt:Key`
- [ ] 6.4 Manual smoke: flag off → register 403/blocked; flag on → first user admin, second user not; login returns a JWT; unauthenticated write → 401; authenticated write → 200 and `CreatedByUserId` set; repeated bad logins → 429/lockout
