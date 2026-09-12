## Why

`add-oidc-sso` declared `REGISTRATION_ENABLED` removed. It was not — it was kept as a
one-boot-per-database bridge so an instance upgrading from before the persisted policy
would keep its registration posture. That bridge has been crossed: the seeder shipped on
2026-09-11 and `:latest` has carried it since, so any live instance has already consumed
the value and written its row.

Keeping it now costs more than it earns, because the app states the same rule twice and
the two statements disagree. For an instance that has users and no settings row:

- `AccountPolicySeeder` writes `LocalRegistrationEnabled = hasUsers ? legacyValue : true`
- `EfAccountPolicy.DefaultForUnseededInstanceAsync` — the answer for exactly that instance
  state when nothing was written — returns `!hasUsers`, i.e. closed

Whether registration is open therefore depends on which code path a reader reaches first.
Retiring the variable deletes the divergence rather than picking a side.

## What Changes

- **`AccountPolicySeeder.SeedAsync` loses its `legacyRegistrationEnabled` parameter.**
  Registration is seeded open only where there is nobody to protect (`!hasUsers`), which
  is what `EfAccountPolicy` already answered for the same state.
- **An instance with users and no settings row now seeds registration closed** rather than
  taking the environment value. Sign-in is still seeded on unconditionally, so nobody is
  locked out; an administrator opens registration from the admin view, and that deliberate
  opening stays open.
- **The variable leaves every surface**: `appsettings.json`, `appsettings.Development.json`,
  the reference `docker-compose.yml`, the documented environment variables in `README.md`,
  and the contributor command in `CLAUDE.md`.
- **A stale CI comment is corrected.** `.github/workflows/ci.yml` claimed the e2e job
  depended on `Development → REGISTRATION_ENABLED = true`. That has been false since the
  seeder shipped: CI starts against a database that does not exist, so the bootstrap branch
  opens registration without consulting the variable at all.

Out of scope: the bootstrap rule itself, the lock-out guard on local sign-in, and the admin
settings surface — all unchanged.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `auth`: the seeding rule loses its environment input, and a populated instance is seeded
  closed instead of inheriting a deploy-time flag.
- `deployment`: the variable leaves the container's documented and default configuration.

`web-client` is untouched — the client already reads the policy from `GET /api/config` and
never knew about the variable.

## Impact

- **Modified projects:** `CoffeeTracker.Infrastructure` (the seeder and its one call site),
  `CoffeeTracker.Api` (two `appsettings` keys), `CoffeeTracker.Tests` (four call sites; one
  behaviour assertion rewritten).
- **No migration.** The settings table and its rows are untouched; only what gets written
  into a *new* row changes.
- **Operator-visible:** an instance that has not yet been started on a build ≥ 2026-09-11
  will seed registration closed instead of inheriting the variable. The fix is one toggle in
  **Admin → Account settings**; sign-in is unaffected, so no one is locked out. Crossing the
  bridge before upgrading — start once on the current image, confirm the admin view — avoids
  even that.
