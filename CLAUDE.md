# Working on this repo

Conventions and hard-won context. The README is the user-facing documentation —
this file is what someone (or some agent) needs to *change* the code without
rediscovering the same things.

## Architecture

Hexagonal, and enforced in that direction: **Domain ← Application ← {Infrastructure, Api}**.

- Controllers depend only on application ports (`Ports/Driving`), **never on EF Core**.
- Adapters live behind driven ports (`Ports/Driven`): `IUserDirectory`, `IPhotoStorage`,
  `IOcrService`, `IRefreshTokenStore`, `ITokenIssuer`…
- DTOs at the boundary. Domain types don't cross the HTTP edge.
- Business rules that an adapter cannot hold belong in the application layer — which
  account an assertion resolves to, who is an administrator, who may edit what.

`backend/Directory.Build.props` carries the shared build settings: `net10.0`, nullable,
.NET analyzers, `TreatWarningsAsErrors` in **Release** only (CI builds Release), and
NuGet lock files.

## Comment style

This codebase comments the **why**, inline, at roughly 25-35% of lines — deliberately
higher than typical. A comment here explains a decision that the code cannot: why a
fallback is a fallback, why a check is ordered where it is, what breaks without it.

Match that density; don't exceed it. Restating what the next line does is noise, and
so is justifying the comment itself.

## Commits & PRs

- Feature branch → PR → CI green → **squash-merge**. `main` is protected.
- PR titles follow **Conventional Commits** — `.github/workflows/pr-title-check.yml`
  enforces it against `.github/conventional-commit-types.json`.
- Commit as `tom.legougaud@gmail.com` on this repo (not the work address).
- `GH_TOKEN` in this environment lacks the `workflow` scope, so for any PR touching
  `.github/workflows/`, use `env -u GH_TOKEN gh ...` and let `gh` use its own auth.

## Testing

See the README's *Running the tests* for the commands. What matters when writing them:

- **Backend integration** tests boot the real app via `WebApplicationFactory<Program>`,
  each against its own throwaway SQLite DB (`ApiFactory` + `ApiClient` helpers).
- **Playwright** starts only the Angular dev server; the API must be running on `:5000`
  against an **empty** database. `e2e/support/global-setup.ts` claims the first account
  (→ administrator) and reopens registration, because a fresh instance allows exactly
  one registration and `fullyParallel` would otherwise make a race decide who gets it.
- `/api/auth` is rate-limited to **10/min**, so e2e specs seed sessions into
  `localStorage` via `injectSession()` rather than logging in repeatedly.
- The e2e OpenID Connect provider (`e2e/support/fake-oidc-provider.ts`) is a real
  minimal server — discovery, JWKS, PKCE, nonce and RSA signatures all genuinely
  happen. A test account on a real provider would be unreachable from CI and would
  make the suite depend on someone else's uptime.
- `provider-sign-in.spec.ts` documents which guards were verified by *reintroducing
  the bug and watching the test go red*, including one that could not be, left
  documented rather than faked. Keep that honesty if you extend it.

## OCR

The adapter **shells out to the `tesseract` CLI**; it is not a P/Invoke NuGet. The
binding proved too brittle on Linux — it probes version-pinned `lib*.dll.so` names and
needs a `libdl` shim.

- `appsettings.Development.json` sets `Ocr:Engine=none` so a bare host without
  `tesseract` doesn't 503-loop; the dev container overrides to `tesseract` via
  `devcontainer.json` `containerEnv`; production uses `appsettings.json`.
- The adapter passes `--tessdata-dir` explicitly: Tesseract 5's CLI treats
  `TESSDATA_PREFIX` as the directory itself, not its parent.
- Arguments go through `ArgumentList` with `UseShellExecute = false`, the image is
  piped over **stdin**, and the language is allowlisted. Keep it that way — no
  caller-controlled value may become an argument.

## Backend dependency bumps

NuGet keeps one lock file per project, so a package change in `Application` or
`Infrastructure` invalidates the ones in `Api` and `Tests` and CI's
`restore --locked-mode` fails with **NU1004**. Refresh them all:
`./scripts/refresh-lockfiles.ps1`, or `gh workflow run refresh-lockfiles.yml -f pr=<n>`
without a local toolchain. That workflow refuses a pull request from a fork by design.

## OpenSpec

Live specs: `auth`, `coffee-catalog`, `label-scan`, `photo-storage`, `reviews`,
`web-client`, `deployment`. Workflow: one change per feature → PR → CI green →
squash-merge → a separate PR archiving the change into `openspec/specs/`.

## Deployment context

Unraid, internet-exposed through SWAG + Authelia. The app keeps **its own** login —
every endpoint requires a token; the reverse proxy is not the authentication. The
production container starts as root, `chown`s `/config` and `/photos` to `PUID:PGID`,
then drops privileges via `gosu`.

## Frontend upgrades — current state

- **`@ngrx/signals`** — *unblocked*. 22.0.1 peers on `@angular/core@^22.0.0` and the
  project runs 22.1.5. The three stores (`AuthStore`, `CoffeesStore`,
  `PhotoCleanupStore`) ship as native-signals stores with the same surface, so the swap
  is a deliberate refactor, not a required update.
- **`lucide-angular`** — *still blocked*. 1.0.0 peers on `13.x - 21.x`. The custom
  `ct-icon` lucide-core wrapper stays until that moves.
- **`openapi-typescript`** runs via `npx` (it peers on TS 5, the project is on TS 6).
  Fine as-is. The e2e CI job regenerates the client from the running backend and fails
  on drift, so a backend contract change cannot ship a stale typed client.

## Gotchas

- **macOS: port 5000 is squatted by AirPlay Receiver.** Either disable it or run the
  API elsewhere (`ASPNETCORE_URLS=http://localhost:5099`) and remap `proxy.conf.json`.
  Stale backgrounded `dotnet run` processes also serve old binaries — kill them first.
- Development happens **in the dev container**; the host is not expected to carry the
  .NET SDK, Node or Tesseract.
