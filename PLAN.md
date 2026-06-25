# Coffee Tracker — Executable Build Plan (M0–M8)

## Context

`PLAN.md` is a thorough greenfield design doc for a self-hosted coffee-cataloging
web app, built as a vehicle to learn modern C#/.NET while using familiar Angular.
The app is meant to be **shared publicly so others can self-host it**, so security
is a first-class requirement and all configuration is environment-driven.

Development happens **inside a dev container** (reproducible toolchain, no host
setup). Deployment is **GitHub Actions → GHCR (public image) → manual install in
the Unraid Docker GUI** — there is no Watchtower / SSH / compose-on-NAS. The repo
`docker-compose.yml` is a local dev/test + reference convenience only, **not** the
NAS deploy path. The target NAS is an Unraid box (UGreen DXP4800 Plus, x86-64), so
images are built for **`linux/amd64` only**.

The repo currently contains `PLAN.md`, `README.md`, `.gitignore`, the
`.devcontainer/`, `.github/workflows/`, `.dockerignore`, `CoffeeTracker.sln`, the
`backend/CoffeeTracker.Tests/` skeleton, and the Unraid template — but no
application code yet. This plan turns the design into a concrete,
milestone-by-milestone build with exact packages, commands, and files, so each
step is independently runnable and teachable.

**Validated against current releases (June 2026):**

- **.NET 10** is the current LTS (Nov 2025) — correct choice.
- **Angular 22** shipped June 2026 with **Signal Forms now stable** (graduated
  from preview in v21) — the plan's frontend assumptions hold.
- **OCR caveat:** the classic `Tesseract` (charlesw) NuGet has documented
  `DllNotFoundException` / libleptonica failures on Linux/.NET. We use the
  maintained **`TesseractOCR`** package instead, plus apt-installed
  `tesseract-ocr` + `-eng` + the `-dev` libraries in **both** the dev container
  and the production image. Engine stays behind `IOcrService` so PaddleOCR/RapidOCR
  can replace it later.
- **Dev container base is Ubuntu Noble.** .NET 10 dropped Debian images, so
  `mcr.microsoft.com/devcontainers/dotnet:1-10.0` is Ubuntu 24.04 "Noble" (there
  is no `1-10.0-bookworm`; `trixie` is preview-only). Fallback tag: `1-10.0-noble`.

**Settled open choices (user said "do what's best"):**

- **API style: Controllers** (clearer grouping across auth/coffees/reviews while
  learning; minimal APIs noted as the modern alternative).
- **Flavor tags: full many-to-many** (`FlavorTag` + `ReviewTag` join) — better
  EF Core learning and the correct model.
- **OCR: `TesseractOCR`** behind `IOcrService`, system English `tessdata`.
- **Registration:** gated by a `REGISTRATION_ENABLED` env flag, **default off**,
  so an internet-exposed instance is not open to abuse. First user to register
  becomes admin (deliberate bootstrap).

---

## Target repo structure

```
coffee-tracker/                 (repo root; dev-container workspace at /workspaces/coffee-tracker)
├─ .devcontainer/               devcontainer.json, Dockerfile (DEV image), post-create.sh
├─ .github/workflows/           ci.yml, release.yml
├─ .dockerignore                keeps node_modules/bin/obj/.git/*.db/photos out of the build context
├─ CoffeeTracker.sln            solution covering the API + Tests
├─ backend/CoffeeTracker.Api/   ASP.NET Core Web API (.NET 10)
│  ├─ Program.cs                DI, EF, Identity/JWT, forwarded headers, static files, routing
│  ├─ Models/                   AppUser, Coffee, Review, FlavorTag, ReviewTag
│  ├─ Data/                     AppDbContext, Migrations/, DbSeeder
│  ├─ Dtos/                     request/response records (separate from entities)
│  ├─ Controllers/             CoffeesController, ReviewsController, AuthController
│  ├─ Services/                 TokenService, PhotoStorage, IOcrService,
│  │                            TesseractOcr, CoffeeLabelParser
│  ├─ appsettings.json          + appsettings.Development.json
│  └─ tessdata/                 (gitignored; only used for the optional bare-metal fallback)
├─ backend/CoffeeTracker.Tests/ xUnit (CoffeeLabelParser, auth/ownership, DTO validation)
├─ frontend/                    Angular 22 PWA
│  └─ src/app/{core,features,shared}/
├─ scripts/                     get-tessdata (OPTIONAL — see M5)
├─ Dockerfile                   PRODUCTION multi-stage (Angular build → API publish → runtime)
├─ docker-compose.yml           local dev/test + reference only (NOT the NAS deploy path)
├─ deploy/unraid/coffee-tracker.xml   Unraid container template
└─ README.md
```

**Two distinct Dockerfiles, no collision:** root `Dockerfile` = **production**
image; `.devcontainer/Dockerfile` = **dev** image. Dev runs inside the container:
the API serves HTTP-only on `localhost:5000` and Angular on `ng serve` (`:4200`)
with a dev proxy + CORS. Prod = single `linux/amd64` container where the API serves
the built Angular static files same-origin.

---

## M0 — Dev container (do this first)

**Goal:** a reproducible toolchain so every later milestone runs inside the
container with no host setup. This is a prerequisite for M1–M8.

The three files already exist in `.devcontainer/`:

1. **`devcontainer.json`** — builds from `.devcontainer/Dockerfile` with the repo
   root as context (`"context": ".."`), adds the **Node 22** feature, forwards
   ports **5000** (API) + **4200** (ng serve), runs `post-create.sh`, sets
   `dotnet.defaultSolution` to `CoffeeTracker.sln`, and uses the non-root `vscode`
   user.
2. **`Dockerfile`** — `FROM mcr.microsoft.com/devcontainers/dotnet:1-10.0` (Ubuntu
   Noble, ships the .NET 10 SDK). Adds the native OCR libraries
   (`tesseract-ocr tesseract-ocr-eng libtesseract-dev libleptonica-dev`) and sets
   `ENV TESSDATA_PREFIX=/usr/share/tesseract-ocr/5`.
3. **`post-create.sh`** — installs `dotnet-ef` (global tool) and
   `@angular/cli@22`, then prints a toolchain summary.

**Why `TESSDATA_PREFIX=/usr/share/tesseract-ocr/5` (not `…/5/tessdata`):**
Tesseract **appends** `/tessdata` to the prefix at runtime. The full prefix would
make it search `…/5/tessdata/tessdata/eng.traineddata` and fail; the parent path is
correct because `eng.traineddata` lives at `…/5/tessdata/`.

**Why the `-dev` packages:** the `TesseractOCR` NuGet P/Invokes the **unversioned**
`libtesseract.so` / `liblept.so` soname, which only the `-dev` packages provide.

**Dev HTTPS decision:** `dotnet dev-certs https --trust` does not reach the host
browser trust store from a Linux container. Run the API **HTTP-only on
`localhost:5000`** in dev — `http://localhost` is a *secure context*, so the PWA
service worker and `getUserMedia` camera still work. There is no `5001`/HTTPS in
the container. M1 and M6 launch settings must reflect this.

**Verify:** VS Code **Reopen in Container** builds cleanly; `post-create.sh` prints
the toolchain summary; `dotnet --info`, `dotnet ef --version`, `ng version` all
succeed; `tesseract --list-langs` includes `eng`.

> Every command in M1–M6 runs **inside the dev container**.

---

## M1 — Backend skeleton + database

**Goal:** a runnable API with one read endpoint and a real SQLite DB.

Steps:

1. `dotnet new webapi -n CoffeeTracker.Api -o backend/CoffeeTracker.Api` (.NET 10).
   Use `--use-controllers` (or convert the minimal template) so we start with
   controllers. Add the project to `CoffeeTracker.sln` and add a `ProjectReference`
   to it from `backend/CoffeeTracker.Tests` (uncomment the block in the test csproj).
2. Add packages: `Microsoft.EntityFrameworkCore.Sqlite`,
   `Microsoft.EntityFrameworkCore.Design`, `Swashbuckle.AspNetCore` (or the
   built-in OpenAPI + a Swagger UI package).
3. `Models/Coffee.cs` — `Id, Name, Roaster, Origin, RoastLevel, Price,
   DateBought, PhotoPath, ShopName, PurchaseUrl, CreatedByUserId, CreatedAt`.
4. `Data/AppDbContext.cs` — `DbSet<Coffee>`; connection string in appsettings.
5. `dotnet ef migrations add InitialCreate` → `dotnet ef database update`
   (file-based `coffee.db`). `dotnet-ef` is already installed by M0.
6. `Controllers/CoffeesController.cs` — `GET /api/coffees` returning a DTO list.
7. Wire Swagger/OpenAPI in `Program.cs`; bind the HTTP-only `localhost:5000`
   profile for dev.

**Learn:** EF Core code-first, migrations, DbContext via DI, appsettings/options.
**Verify:** `dotnet watch run` → Swagger UI at `http://localhost:5000` lists/returns
coffees.

---

## M2 — Coffee CRUD + photo upload

1. `Dtos/` records: `CoffeeCreateDto`, `CoffeeUpdateDto`, `CoffeeResponseDto`
   with DataAnnotations validation (`[Required]`, ranges, URL).
2. Full CRUD in `CoffeesController` (GET one, POST, PUT, DELETE), all `async`,
   mapping DTO↔entity.
3. `Services/PhotoStorage.cs` — save uploads to a configurable `photos/` dir,
   return a relative path; **content-type allowlist + size cap + randomized
   filenames + path-traversal guards** (see Security); serve from a
   non-executable static path.
4. `POST /api/coffees/{id}/photo` (multipart) → store + set `PhotoPath`; serve
   images via static files (`/photos/...`) or a streaming endpoint.

**Learn:** model binding, validation pipeline, `IFormFile` handling, async EF.
**Verify:** create/update/delete a coffee in Swagger; upload a photo and load it
back by URL; oversized / wrong-type uploads are rejected.

---

## M3 — Auth (Identity + JWT)

1. Add `Microsoft.AspNetCore.Identity.EntityFrameworkCore` +
   `Microsoft.AspNetCore.Authentication.JwtBearer`.
2. `Models/AppUser : IdentityUser` (adds `IsAdmin`, `DisplayName`). Switch
   `AppDbContext` to `IdentityDbContext<AppUser>`; add a migration.
3. `Services/TokenService.cs` — issue JWT with user id + `IsAdmin` claims.
   **The signing key comes only from configuration/env; the app fails to start if
   it is missing or weak** (no baked-in default — see Security).
4. `Controllers/AuthController.cs` — `POST /register`, `POST /login`. Registration
   is gated by the **`REGISTRATION_ENABLED` flag (default off)**; when the first
   user does register, set `IsAdmin = true`. Apply an Identity **password policy +
   lockout** and **rate-limit** `register`/`login`.
5. `Program.cs` — add Identity, JWT bearer auth, authorization. Mark write
   endpoints `[Authorize]`; stamp `CreatedByUserId` from the token.

**Learn:** password hashing (Identity), claims, JWT validation, auth policies,
rate-limiting middleware.
**Verify:** with the flag on, register two users (user 1 = admin); with the flag
off, registration is refused; unauthenticated writes → 401; authenticated writes
succeed; repeated bad logins trip the rate limit.

---

## M4 — Reviews, ratings & flavor tags

1. `Models/Review.cs` — `Id, CoffeeId, UserId, Rating(1–5), TastingNotes,
   BrewMethod, Grind, Ratio, CreatedAt, UpdatedAt`. **Unique index on
   (CoffeeId, UserId)** to enforce one review per user per coffee.
2. `Models/FlavorTag.cs` + `ReviewTag` join → many-to-many Review↔FlavorTag;
   seed a starter tag set (fruity, chocolatey, nutty, floral, …) in `DbSeeder`.
3. `Controllers/ReviewsController.cs` — create/update _your own_ review (ownership
   check from token), list reviews for a coffee, compute **average rating**.
4. Extend `CoffeeResponseDto` (or detail endpoint) with `AverageRating` +
   `ReviewCount`.

**Learn:** EF relationships (1-many + many-many), unique constraints, ownership
authorization, aggregate queries.
**Verify:** both users review one coffee; average + per-user reviews correct;
editing another user's review is rejected.

---

## M5 — Snap-to-fill OCR (backend)

1. `Services/IOcrService.cs` — `Task<OcrResult> ReadAsync(Stream image)`.
2. `Services/TesseractOcr.cs` using the **`TesseractOCR`** NuGet. Standardize on
   the **system tessdata installed via apt** (`/usr/share/tesseract-ocr/5/tessdata`,
   reached through `TESSDATA_PREFIX`) in both dev and prod. `Ocr:TessdataPath`
   defaults to the system path and only overrides when explicitly set;
   `Ocr:Engine` selects the impl so a future PaddleOCR/RapidOCR is a config swap.
3. `Services/CoffeeLabelParser.cs` — heuristics + regex turning raw OCR text into
   best-effort fields (name, roaster, origin, roast level, weight).
4. `POST /api/coffees/scan` (multipart) → `{ rawText, parsed fields }`. Does NOT
   create a coffee; it pre-fills the form. The uploaded photo is retained for
   reuse as the coffee image.
5. `scripts/get-tessdata` is **OPTIONAL** — only for running bare-metal without the
   apt packages; it downloads `eng.traineddata` into the gitignored `tessdata/`.
   The container path (dev + prod) does not need it.

**Learn:** interfaces + DI for swappable impls, image stream handling, options
binding.
**Verify:** POST a bag photo to `/scan`; confirm text extraction + parsed fields;
try a few real bags to gauge accuracy (swap point if weak).

---

## M6 — Angular 22 PWA + snap-to-fill UX

1. `ng new` (Angular 22, standalone, signals) in `frontend/`; add
   `@angular/pwa` (manifest, service worker, icons, offline shell).
2. `core/` — `auth.service.ts` (signals for current user/token, localStorage),
   `auth.interceptor.ts` (attach JWT), `auth.guard.ts`, typed models.
3. `features/auth/` — login + register using **Signal Forms** (hide/disable
   register when `REGISTRATION_ENABLED` is off, surfaced via a config endpoint).
4. `features/coffees/` — list (search + avg rating), detail (your review +
   everyone's + average), add/edit (Signal Forms), photo display.
5. **Snap-to-fill:** `<input type="file" accept="image/*" capture="environment">`
   → POST `/api/coffees/scan` → pre-fill the add form from the response → user
   reviews/edits → save (keep the photo as the coffee image).
6. Dev proxy (`proxy.conf.json`) so `ng serve` (`:4200`) talks to the API on
   `http://localhost:5000`; matching CORS in the API **dev** config only.
7. **CI cleanup:** now that `frontend/` exists, drop the `frontend/package.json`
   existence guard in `ci.yml` (the `check` step + the `if:` on each frontend
   step) so the job always runs its real steps.

**Learn:** standalone components, signals state, Signal Forms, HTTP interceptors,
route guards, PWA install/offline.
**Verify:** `ng serve` → login/register, list/detail/add flows work against the
API; PWA installable (over HTTPS in prod / `http://localhost` in dev); camera
opens on a phone.

---

## M7 — Production image + local compose

1. Multi-stage root **`Dockerfile`** (production):
   - Stage 1: `node:22` → `ng build` (production static files).
   - Stage 2: .NET 10 SDK → `dotnet publish` the API.
   - Stage 3: ASP.NET 10 runtime on **Ubuntu Noble** →
     `apt-get install -y tesseract-ocr tesseract-ocr-eng libtesseract-dev
     libleptonica-dev` (the **same** OCR set as dev), set
     `ENV TESSDATA_PREFIX=/usr/share/tesseract-ocr/5`, copy the published API +
     Angular `dist` (served as same-origin static files). **Run as a non-root
     user**; make only `/config` + `/photos` writable.
2. `Program.cs` prod wiring: configure **`UseForwardedHeaders`** (honor
   `X-Forwarded-Proto`, set `KnownProxies`/`KnownNetworks`) so auth redirects /
   Secure cookies behave behind the reverse proxy; enable **HSTS**. Run
   `db.Database.Migrate()` on startup **scoped to single-instance prod** — note the
   SQLite write-lock and no-rollback hazard (back up first; see Security).
3. **`docker-compose.yml`** — one service, named volumes for `coffee.db`
   (`/config`) and `/photos`; documents the env vars (JWT key, connection string,
   `REGISTRATION_ENABLED`). This is **local dev/test + reference only** and is
   explicitly **not** how the NAS runs the app.
4. `.dockerignore` keeps `node_modules`, `bin`/`obj`, `.git`, the dev SQLite DB,
   and photos out of the build context.
5. **CI cleanup:** now that the `Dockerfile` exists, drop the `Dockerfile`
   existence guard in `ci.yml` (the `check` step + the `if:` on `setup-buildx`
   and `build-push`) so `docker-build` always builds for real.

**Verify:** `docker compose up --build`; hit the single container; OCR `/scan`
works inside the image; data + photos persist across `down/up`.

---

## M8 — CI/CD (GitHub Actions → GHCR) + Unraid install

**Canonical image ref:** `ghcr.io/thomas-lg/coffee-tracker`. Tag scheme (from
`docker/metadata-action`): `latest` (default branch), `sha-<short>`, and semver on
`v*` tags. The same ref/scheme is used by compose, local buildx, CI, and the Unraid
template.

1. **`.github/workflows/ci.yml`** (on `pull_request` + `push: main`) — committed
   up front in M0 and **scaffolding-aware**, so CI stays green at every milestone:
   - backend: `dotnet restore/build/test` on `CoffeeTracker.sln`; the runner
     installs the OCR apt packages and sets `TESSDATA_PREFIX` so OCR tests run.
   - frontend: `npm ci`, `ng lint` (if present), `ng build --configuration
     production`, `ng test --watch=false --browsers=ChromeHeadless` — but only
     **once `frontend/` exists (M6)**; until then the job is a green no-op.
   - docker-build: `docker build` (no push) of the production Dockerfile for
     `linux/amd64` — only **once the Dockerfile exists (M7)**; until then a no-op.
   - The frontend/docker-build job names always report so they can serve as
     required status checks from day one (see branch protection below).
2. **`.github/workflows/release.yml`** (on `push: main` + tags `v*`): build & push
   `linux/amd64` to GHCR via `docker/login-action` (`${{ github.actor }}` +
   `${{ secrets.GITHUB_TOKEN }}`) with `permissions: { contents: read, packages:
   write }`. The package is set **public** in GHCR settings.
3. **Deploy = manual.** Install/update via the **Unraid Docker GUI** pointed at the
   GHCR image, using **`deploy/unraid/coffee-tracker.xml`** (WebUI port, `/config` +
   `/photos` volumes under `/mnt/user/appdata/coffee-tracker/`, env vars with the
   required JWT key and `REGISTRATION_ENABLED`). No Watchtower/SSH/self-hosted
   runner.

**Verify:** open a PR → `ci.yml` green. Merge to `main` / push a `v*` tag →
`release.yml` publishes a `linux/amd64` image to GHCR with `latest`/`sha`/semver
tags; package is public. Install on Unraid from the template → reachable behind the
reverse proxy over HTTPS; `coffee.db` + photos persist across an image update.

---

## Security (cross-cutting — threads through M2/M3/M7/M8)

Because instances may be **internet-exposed and shared**:

- **No default JWT signing key.** The app **fails to start** if the signing-key env
  var is missing/weak — never a baked-in default. Key + connection string are
  injected at runtime only (env / Unraid template), never in the image or git.
- **HTTPS enforced at the NAS reverse proxy** (NPM/SWAG/Traefik/Caddy + auto-renewed
  Let's Encrypt). Configure `UseForwardedHeaders` (`X-Forwarded-Proto`,
  `KnownProxies`/`KnownNetworks`), enable HSTS, and keep the container on the proxy
  network rather than publishing its port directly on the LAN.
- **Auth hardening (M3):** Identity password policy + lockout; rate-limit
  `register`/`login`; `REGISTRATION_ENABLED` flag (default off) gates open signup;
  first user becomes admin as a deliberate bootstrap.
- **CORS:** prod serves the SPA same-origin → **no CORS in prod**; CORS only in the
  dev profile.
- **Uploads (M2):** content-type allowlist + size cap + randomized filenames +
  path-traversal guards; serve from a non-executable static path.
- **Container:** non-root user, read-only root FS where feasible, only `/config` +
  `/photos` writable.
- **Supply chain:** enable Dependabot; optionally add CodeQL + a Trivy image-scan
  step in CI.
- **Backups:** `coffee.db` + `photos/` (Unraid `/config` + `/photos`) should be
  backed up **before pulling a new image**, since a bad startup auto-migration has
  no rollback.

---

## Cross-cutting conventions

- **Comments:** concept notes inline (the .NET "why") per the learning goal.
- **Config/secrets:** dev values in appsettings; prod via env vars (JWT key,
  connection string, OCR paths, `REGISTRATION_ENABLED`). Never commit real keys.
- **DTOs everywhere** at the API boundary — entities never serialized directly.
- **Git flow:** commit per milestone on a feature branch; open a PR → CI (`ci.yml`)
  runs on the PR; merge to `main` (or push a `v*` tag) triggers `release.yml` and
  publishes the image. Don't push/merge unless asked.
- **Branch protection (`main`):** PR required before merge, with the `backend`,
  `frontend`, and `docker-build` checks green and up to date; **linear history**
  (squash- or rebase-merge only — no merge commits); force-push and deletion
  blocked; **0 required approvals** (solo maintainer self-merges). Not enforced for
  admins. Requires a public repo or GitHub Pro to stay enabled.

## End-to-end verification

- **Dev container:** Reopen in Container builds clean; toolchain summary prints;
  `tesseract --list-langs` includes `eng`.
- **Auth:** registration flag enforced; user 1 admin; per-user review isolation;
  401 on anon write; rate limit trips.
- **Data:** coffee + photo + reviews from both users → correct average + per-user.
- **Snap-to-fill:** phone/`capture` upload → text extracted → form pre-fills.
- **PWA:** HTTPS load → Add to Home Screen → fullscreen → camera opens.
- **Image/CI:** PR green; `main`/tag → public `linux/amd64` GHCR image;
  `docker run` it with env config → API serves the SPA, `/scan` returns text.
- **Unraid:** install via the template behind a reverse proxy; persistence across
  an image update.

## Notable risks

- **OCR accuracy on real bags** is the biggest unknown; `IOcrService` is the
  designed escape hatch to PaddleOCR/RapidOCR.
- **Tesseract native libs** — mitigated by `TesseractOCR` + the `-dev` soname
  packages + correct `TESSDATA_PREFIX`, kept identical in dev and prod.
- **Floating `1-10.0` dev-container tag** can drift; pin a digest if reproducibility
  matters later.
- **EF auto-migrate on prod startup** has no rollback and assumes a single
  instance — back up `coffee.db` before updating the image.
- **Public-instance abuse surface** — mitigated by the registration flag,
  rate-limiting, HTTPS-at-proxy, and non-root/least-writable container.
- **Bleeding-edge versions** (.NET 10 / Angular 22 / Signal Forms): pin exact
  versions at scaffold time and verify the `TesseractOCR` package targets .NET 10.
