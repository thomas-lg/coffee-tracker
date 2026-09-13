# Test plan — retiring REGISTRATION_ENABLED

The change is small; the risk is concentrated in one place — what gets written into a
settings row that does not exist yet. Two of the phases below are manual because nothing in
CI starts against a database that already has users.

## Phase 1 — Automated suites

1. `dotnet test CoffeeTracker.sln` — green.
   - `AccountPolicyTests` must **fail to compile** before task 3.1 is done. That failure is
     the proof the parameter is genuinely gone rather than defaulted.
   - `LocalAccountPolicyTests` (6 tests) covers the fresh-install bootstrap end to end over
     real HTTP, including the two `stampPolicy: false` cases that let the seeder run.
2. `grep -rn REGISTRATION_ENABLED . --exclude-dir={.git,bin,obj,node_modules,dist,archive}`
   — returns only `specs/deployment/spec.md` in this change (the "setting it changes
   nothing" scenario).

## Phase 2 — A fresh instance, by hand

Proves the bootstrap still works with nothing configured.

1. `rm -f backend/CoffeeTracker.Api/coffee.db*`
2. `ASPNETCORE_ENVIRONMENT=Development dotnet run --project backend/CoffeeTracker.Api`
3. `GET /api/config` → `registrationEnabled: true`
4. `POST /api/auth/register` → 200, and the account comes back `isAdmin: true`
5. `POST /api/auth/register` again → 403
6. `GET /api/config` → `registrationEnabled: false`

## Phase 3 — The upgrade path, by hand

The one CI cannot reach: a database with users and no settings row.

1. Take a copy of a database that has accounts; `sqlite3 copy.db "DELETE FROM AppSettings;"`
2. Point `ConnectionStrings__Default` at the copy and start the API
3. An existing account signs in — **this is the assertion that matters**; seeding sign-in
   off would lock the instance out
4. `GET /api/config` → `registrationEnabled: false`
5. Open registration in **Admin → Account settings**, restart, and confirm it is still open
   (the seeder is a one-shot and must not overwrite it)

## Phase 4 — The container

1. `docker compose down -v`
2. `JWT_KEY=$(openssl rand -base64 48) docker compose up -d --build` — no other variable
3. Register at `:8080` — proves the compose file needs nothing that was just removed

## Phase 5 — Regression surface

- The Playwright suite against a freshly started API: `global-setup.ts` claims the first
  account and reopens registration, which exercises the bootstrap and the admin settings
  endpoint together.
- `ConfigEndpointTests` and `AuthFlowTests` — both stamp the policy explicitly, so they
  should be unaffected; if either moves, the seeding change reached further than intended.
