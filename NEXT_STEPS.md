# Next steps & handoff

Working notes to resume on another machine. The **M0–M8 build is complete and
merged**; what remains is operational setup, a couple of optional follow-ups, and a
parking lot of ideas (see the README's "Ideas for later").

## Where things stand

- **Done on `main`:** M0 + CI/CD, M1 (catalog + hexagonal + SQLite WAL), M2 (CRUD +
  photo upload), M3 (auth: Identity + JWT, hardened), M4 (reviews/ratings/tags),
  M5 (snap-to-fill OCR backend), **M6** (Angular 22 PWA), **M7** (production image +
  compose), **M8** (CI/CD → GHCR + Unraid template).
- **Beyond the plan, also merged:** ratings **over time** (multiple dated reviews per
  coffee — overturned M4's one-per-user model), **admin photo-cleanup** (backend
  `GET`/`DELETE /api/admin/photos` + the Angular admin table), **non-root PUID/PGID**
  container for Unraid bind mounts, **CodeQL + Trivy** scanning, **Dependabot**,
  build-provenance attestation, and an HTTP **integration test** for the admin
  authorization policy.
- **OpenSpec specs (live):** `auth`, `coffee-catalog`, `label-scan`, `photo-storage`,
  `reviews`, `web-client`, `deployment`. Workflow: change per feature → PR → CI green
  → squash-merge → separate PR archiving the change into `openspec/specs/`.

## What's left

### Operational (GitHub / NAS — no code)
- **GHCR package public** — flip the `coffee-tracker` package visibility to public so
  the NAS can pull without auth.
- **Branch protection on `main`** — PR required; `backend`/`frontend`/`docker-build`
  checks green + up to date; linear history (squash/rebase only); block force-push +
  deletion; 0 required approvals (solo); not enforced for admins. (Needs a public repo
  or GitHub Pro.)
- **Unraid install** — add the container from `deploy/unraid/my-coffee-tracker.xml`
  via the Docker GUI, behind the SWAG/Authelia reverse proxy; set `Jwt__Key`,
  `REGISTRATION_ENABLED` (briefly, to create the admin), `ForwardedHeaders__KnownProxies`,
  and `PUID`/`PGID` to match the host appdata owner.

### Optional follow-ups
- **Dependabot `node:26-slim` PR** — open; the project is standardised on Node 22
  (devcontainer + CI + build stage). Either close it or bump Node project-wide
  deliberately; don't merge the lone image bump.
- **Deferred Angular upgrades** — see the section below.
- **Feature ideas** — parked in the README's "Ideas for later".

## Context / gotchas to remember

- **Local smoke tests: do NOT use port 5000** — macOS AirPlay squats it and stale
  backgrounded `dotnet run` processes serve old binaries. Run with
  `ASPNETCORE_URLS=http://localhost:5099 dotnet run --no-launch-profile --no-build`.
- **OCR on the host:** `Ocr:Engine=none` in `appsettings.Development.json` so the host
  runs without native libs (`/api/coffees/scan` → 503). Real OCR runs in the dev
  container / prod image. Locally: `brew install tesseract` + `Ocr__Engine=tesseract`.
- **Commit email** for this repo is `tom.legougaud@gmail.com` (not the work email).
- **Git over HTTPS in the dev container:** the remote is HTTPS and auth is forwarded by
  the VS Code Dev Containers credential helper (no SSH key in the container). `gh` is
  not installed — PRs/merges are done via the GitHub REST API with that token.
- **Deployment:** Unraid, internet-exposed via SWAG + Authelia. The app keeps its
  **own** login (all endpoints require a token). The prod container runs **non-root**:
  it starts as root, chowns `/config` + `/photos` to `PUID:PGID`, then drops via gosu.
- **Conventions:** hexagonal (controllers depend only on application ports, never EF);
  DTOs at the boundary; JWT signing key from env only (fail-fast, no baked default);
  concept-note comments inline; squash-merge with a Conventional-Commit PR title.

## Deferred frontend upgrades (Angular 22 ahead of the ecosystem)

Wanted but blocked by libraries lagging Angular 22 — revisit when compatible releases
land:

- **NgRx SignalStore (`@ngrx/signals`)** for the stateful stores (`AuthStore`,
  `CoffeesStore`, `PhotoCleanupStore`). Today it peers on `@angular/core@^21`; we ship
  native-signals stores with the same surface, so the swap is low-risk later.
- **`lucide-angular`** to replace the custom `ct-icon` lucide-core wrapper, once it
  supports Angular 22.
- Minor: `openapi-typescript` runs via `npx` (peers on TS 5 vs our TS 6) — fine to
  leave. Also re-run `npm run gen:api` so the admin photo DTOs gain their `api-types`
  drift guards once the OpenAPI doc includes `/api/admin/photos`.
