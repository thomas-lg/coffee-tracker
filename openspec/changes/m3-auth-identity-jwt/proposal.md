## Why

The catalog is currently world-writable: anyone who can reach the API can create, edit, or delete coffees, and `CreatedByUserId` is always null. Milestone M3 in `PLAN.md` adds authentication and authorization so the app can be safely internet-exposed and shared: users register/log in, write operations require a valid token, and each coffee records who created it — the foundation reviews & per-user ratings (M4) build on.

## What Changes

- Add ASP.NET Core **Identity** (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`) and **JWT bearer** auth (`Microsoft.AspNetCore.Authentication.JwtBearer`).
- Add `AppUser : IdentityUser` (with `IsAdmin`, `DisplayName`) and switch `AppDbContext` to `IdentityDbContext<AppUser>`; add an EF migration creating the Identity tables.
- New `auth` capability behind a driving port `IAuthService`: `POST /api/auth/register` and `POST /api/auth/login`, returning a signed JWT (user id + `IsAdmin` claim) on success.
- **Registration is gated by `REGISTRATION_ENABLED` (default off).** When enabled and the registration succeeds, the **first** user created becomes admin (`IsAdmin = true`); subsequent users do not.
- **JWT signing key comes only from configuration/env (`Jwt:Key`); the app fails fast at startup if it is missing or too weak** — no baked-in default.
- Apply an Identity **password policy + account lockout**, and **rate-limit** `register`/`login` with the built-in rate limiter.
- Protect catalog **write** endpoints (`POST`/`PUT`/`DELETE /api/coffees`, `POST /api/coffees/{id}/photo`) with `[Authorize]`; reads stay public. On create, stamp `CreatedByUserId` from the caller's token via an `ICurrentUser` port.

Out of scope (deferred): reverse-proxy concerns — `UseForwardedHeaders`/HSTS (M7); CORS (no prod CORS; dev CORS arrives with the frontend, M6); roles beyond a single `IsAdmin` flag; refresh tokens / token revocation; email confirmation / password reset.

## Capabilities

### New Capabilities
- `auth`: registering and authenticating users and issuing JWTs that authorize catalog writes.

### Modified Capabilities
- `coffee-catalog`: write operations now require authentication; created coffees record their owner.

## Impact

- **New packages:** `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.Authentication.JwtBearer`.
- **Modified projects:** `CoffeeTracker.Application` (auth driving port + DTOs, `ICurrentUser` driven port, owner stamping in `CoffeeCatalogService`), `CoffeeTracker.Infrastructure` (`AppUser`, `IdentityDbContext`, `IdentityAuthService`, `TokenService`, migration, DI), `CoffeeTracker.Api` (`AuthController`, `[Authorize]` on writes, JWT/Identity/rate-limiter wiring, `ICurrentUser` via `IHttpContextAccessor`).
- **New EF migration:** adds the ASP.NET Identity tables (`AspNetUsers`, `AspNetRoles`, …). Applied on startup like the existing migration.
- **Configuration:** new `Jwt` section (`Key` required via env `Jwt__Key`; `Issuer`/`Audience` with defaults) and `REGISTRATION_ENABLED` (default `false`). Dev `appsettings.Development.json` may carry a throwaway dev key; production injects `Jwt__Key` via env only — never committed.
- **Behaviour change:** existing unauthenticated clients calling write endpoints now get `401` instead of `200`.
