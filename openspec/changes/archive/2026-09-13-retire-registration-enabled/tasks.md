## 1. Seeding

- [x] 1.1 Drop the `legacyRegistrationEnabled` parameter from `AccountPolicySeeder.SeedAsync`
- [x] 1.2 Seed `LocalRegistrationEnabled = !hasUsers`, keeping it a separate assignment from `RegistrationOpenedForBootstrap`
- [x] 1.3 Rewrite the doc comment: why sign-in is always on, why a populated instance starts shut, and that it now matches `EfAccountPolicy`
- [x] 1.4 Collapse the call site in `DependencyInjection.InitializeDatabaseAsync` to `SeedAsync(db, ct)`

## 2. Configuration surface

- [x] 2.1 Remove the key from `appsettings.json`
- [x] 2.2 Remove the key from `appsettings.Development.json`
- [x] 2.3 Remove the variable and its comment block from `docker-compose.yml`

## 3. Tests

- [x] 3.1 Update the four `SeedAsync` call sites in `AccountPolicyTests`
- [x] 3.2 Replace the `[Theory]` over the legacy value with a `[Fact]` asserting a populated instance is seeded sign-in on, registration off, bootstrap flag off
- [x] 3.3 Note in that test's comment that it is the only automated cover for the upgrade path
- [x] 3.4 Correct the stale comments in `ApiFactory`, `AuthFlowTests` and `LocalAccountPolicyTests`

## 4. Documentation

- [x] 4.1 Delete the table row from `README.md`
- [x] 4.2 Drop the variable from the `CLAUDE.md` screenshot command, and add the `docker compose down -v` note that was missing
- [x] 4.3 Correct the `ci.yml` comment, which described a dependency on the variable that has not existed since the seeder shipped
- [x] 4.4 Leave `PLAN.md` alone — it is the historical build plan and editing it would make it misreport its own history

## 5. Verification

- [x] 5.1 `dotnet test CoffeeTracker.sln` green — run by CI (`ci.yml` builds and tests the solution in Release); green on the merge commit of #133
- [x] 5.2 `grep -rn REGISTRATION_ENABLED` returns only this change's deployment delta — confirmed: the only hits outside `PLAN.md` are the two lines in `specs/deployment/spec.md` that declare the variable inert

The three below need a running instance. They were **not** performed: the only
Docker host available cannot start (its WSL backend is unresponsive), and ticking
them off unverified would make this record lie about what was checked.

- [ ] 5.3 Fresh install by hand: register once (admin), second attempt refused, `/api/config` reports registration closed
- [ ] 5.4 Upgrade path by hand: a database with users and no settings row starts with sign-in on and registration off, and an administrator can open it durably
- [ ] 5.5 `docker compose down -v && docker compose up -d --build` with only `JWT_KEY` set
