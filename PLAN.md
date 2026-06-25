# Coffee Tracker — Executable Build Plan (M1–M7)

## Context

`PLAN.md` is a thorough greenfield design doc for a self-hosted coffee-cataloging
web app (NAS + Docker), built as a vehicle to learn modern C#/.NET while using
familiar Angular. The repo currently contains only `PLAN.md`, `README.md`, and
`.gitignore` — no code yet. This plan turns that design into a concrete,
milestone-by-milestone build with exact packages, commands, and files, so each
step is independently runnable and teachable.

**Validated against current releases (June 2026):**

- **.NET 10** is the current LTS (Nov 2025) — correct choice.
- **Angular 22** shipped June 2026 with **Signal Forms now stable** (graduated
  from preview in v21) — the plan's frontend assumptions hold.
- **OCR caveat:** the classic `Tesseract` (charlesw) NuGet has documented
  `DllNotFoundException` / libleptonica failures on Linux/.NET. We use the
  maintained **`TesseractOCR`** package instead, plus apt-installed
  `tesseract-ocr` + `libtesseract-dev` in the Docker image. Engine stays behind
  `IOcrService` so PaddleOCR/RapidOCR can replace it later.

**Settled open choices (user said "do what's best"):**

- **API style: Controllers** (clearer grouping across auth/coffees/reviews while
  learning; minimal APIs noted as the modern alternative).
- **Flavor tags: full many-to-many** (`FlavorTag` + `ReviewTag` join) — better
  EF Core learning and the correct model.
- **OCR: `TesseractOCR`** behind `IOcrService`, English `tessdata` bundled.

---

## Target repo structure

```
coffee-tracker/                 (= repo root /home/user/repo)
├─ backend/CoffeeTracker.Api/   ASP.NET Core Web API (.NET 10)
│  ├─ Program.cs                DI, EF, Identity/JWT, CORS, static files, routing
│  ├─ Models/                   AppUser, Coffee, Review, FlavorTag, ReviewTag
│  ├─ Data/                     AppDbContext, Migrations/, DbSeeder
│  ├─ Dtos/                     request/response records (separate from entities)
│  ├─ Controllers/             CoffeesController, ReviewsController, AuthController
│  ├─ Services/                 TokenService, PhotoStorage, IOcrService,
│  │                            TesseractOcr, CoffeeLabelParser
│  ├─ appsettings.json          + appsettings.Development.json
│  └─ tessdata/eng.traineddata  (gitignored; fetched by scripts/get-tessdata)
├─ frontend/                    Angular 22 PWA
│  └─ src/app/{core,features,shared}/
├─ scripts/                     helper scripts (mirrors plex-releases-summary style)
├─ Dockerfile                   multi-stage: Angular build → API publish → runtime
├─ docker-compose.yml           1 service + volumes (db, photos)
└─ README.md
```

Dev = two processes (`dotnet watch` + `ng serve`, CORS enabled). Prod = single
container where the API serves the built Angular static files.

---

## M1 — Backend skeleton + database

**Goal:** a runnable API with one read endpoint and a real SQLite DB.

Steps:

1. `dotnet new webapi -n CoffeeTracker.Api -o backend/CoffeeTracker.Api` (.NET 10).
   Use `--use-controllers` (or convert the minimal template) so we start with
   controllers.
2. Add packages: `Microsoft.EntityFrameworkCore.Sqlite`,
   `Microsoft.EntityFrameworkCore.Design`, `Swashbuckle.AspNetCore` (or the
   built-in OpenAPI + a Swagger UI package).
3. `Models/Coffee.cs` — `Id, Name, Roaster, Origin, RoastLevel, Price,
DateBought, PhotoPath, ShopName, PurchaseUrl, CreatedByUserId, CreatedAt`.
4. `Data/AppDbContext.cs` — `DbSet<Coffee>`; connection string in appsettings.
5. `dotnet ef migrations add InitialCreate` → `dotnet ef database update`
   (file-based `coffee.db`). Install `dotnet-ef` tool if needed.
6. `Controllers/CoffeesController.cs` — `GET /api/coffees` returning a DTO list.
7. Wire Swagger/OpenAPI in `Program.cs`.

**Learn:** EF Core code-first, migrations, DbContext via DI, appsettings/options.
**Verify:** `dotnet watch run` → Swagger UI lists/returns coffees.

---

## M2 — Coffee CRUD + photo upload

1. `Dtos/` records: `CoffeeCreateDto`, `CoffeeUpdateDto`, `CoffeeResponseDto`
   with DataAnnotations validation (`[Required]`, ranges, URL).
2. Full CRUD in `CoffeesController` (GET one, POST, PUT, DELETE), all `async`,
   mapping DTO↔entity.
3. `Services/PhotoStorage.cs` — save uploads to a configurable `photos/` dir,
   return a relative path; guard content-type/size; generate unique filenames.
4. `POST /api/coffees/{id}/photo` (multipart) → store + set `PhotoPath`; serve
   images via static files (`/photos/...`) or a streaming endpoint.

**Learn:** model binding, validation pipeline, `IFormFile` handling, async EF.
**Verify:** create/update/delete a coffee in Swagger; upload a photo and load it
back by URL.

---

## M3 — Auth (Identity + JWT)

1. Add `Microsoft.AspNetCore.Identity.EntityFrameworkCore` +
   `Microsoft.AspNetCore.Authentication.JwtBearer`.
2. `Models/AppUser : IdentityUser` (adds `IsAdmin`, `DisplayName`). Switch
   `AppDbContext` to `IdentityDbContext<AppUser>`; add a migration.
3. `Services/TokenService.cs` — issue JWT with user id + `IsAdmin` claims; signing
   key/issuer/audience from appsettings (env-overridable in prod).
4. `Controllers/AuthController.cs` — `POST /register`, `POST /login`. On register,
   if it's the first user, set `IsAdmin = true`.
5. `Program.cs` — add Identity, JWT bearer auth, authorization. Mark write
   endpoints `[Authorize]`; stamp `CreatedByUserId` from the token.

**Learn:** password hashing (Identity), claims, JWT validation, auth policies.
**Verify:** register two users (user 1 = admin); unauthenticated writes → 401;
authenticated writes succeed.

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
2. `Services/TesseractOcr.cs` using the **`TesseractOCR`** NuGet; load English
   `tessdata`; path/engine selected from appsettings (`Ocr:Engine`,
   `Ocr:TessdataPath`) so a future PaddleOCR/RapidOCR impl is a config swap.
3. `Services/CoffeeLabelParser.cs` — heuristics + regex turning raw OCR text into
   best-effort fields (name, roaster, origin, roast level, weight).
4. `POST /api/coffees/scan` (multipart) → `{ rawText, parsed fields }`. Does NOT
   create a coffee; it pre-fills the form. The uploaded photo is retained for
   reuse as the coffee image.
5. `scripts/get-tessdata` to download `eng.traineddata` into `tessdata/`.

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
3. `features/auth/` — login + register using **Signal Forms**.
4. `features/coffees/` — list (search + avg rating), detail (your review +
   everyone's + average), add/edit (Signal Forms), photo display.
5. **Snap-to-fill:** `<input type="file" accept="image/*" capture="environment">`
   → POST `/api/coffees/scan` → pre-fill the add form from the response → user
   reviews/edits → save (keep the photo as the coffee image).
6. Dev proxy (`proxy.conf.json`) so `ng serve` talks to the API; matching CORS in
   the API dev config.

**Learn:** standalone components, signals state, Signal Forms, HTTP interceptors,
route guards, PWA install/offline.
**Verify:** `ng serve` → login/register, list/detail/add flows work against the
API; PWA installable (over HTTPS); camera opens on a phone.

---

## M7 — Dockerize + NAS deploy

1. Multi-stage `Dockerfile`:
   - Stage 1: `node` → `ng build` (production static files).
   - Stage 2: .NET SDK → `dotnet publish` the API.
   - Stage 3: ASP.NET runtime → `apt-get install tesseract-ocr libtesseract-dev`,
     copy published API + Angular `dist` (served as static files) + `tessdata`.
2. `docker-compose.yml` — one service, named volumes for `coffee.db` and
   `/photos`; JWT signing key + connection string via env/secret.
3. Run EF migrations on startup (`db.Database.Migrate()` in `Program.cs`) so the
   volume DB is created/updated automatically.
4. `README.md` NAS section (Synology/QNAP/Unraid Compose) incl. the **HTTPS note**
   — PWA + camera need a secure context, terminated at the NAS reverse proxy /
   Let's Encrypt.

**Verify:** `docker compose up --build`; hit the single container; data + photos
persist across `docker compose down/up` (volumes work).

---

## Cross-cutting conventions

- **Comments:** concept notes inline (the .NET "why") per the learning goal.
- **Config/secrets:** dev values in appsettings; prod via env vars (JWT key,
  connection string, OCR paths). Never commit real keys.
- **DTOs everywhere** at the API boundary — entities never serialized directly.
- **Commit per milestone** on a feature branch (don't commit/push unless asked).

## End-to-end verification (from PLAN.md)

- **Auth:** two users, user 1 admin, per-user review isolation, 401 on anon write.
- **Data:** coffee + photo + reviews from both users → correct average + per-user.
- **Snap-to-fill:** phone/`capture` upload → text extracted → form pre-fills.
- **PWA:** HTTPS load → Add to Home Screen → fullscreen → camera opens.
- **Container:** `docker compose up --build` → persistence across down/up.

## Notable risks

- **OCR accuracy on real bags** is the biggest unknown; `IOcrService` is the
  designed escape hatch to PaddleOCR/RapidOCR.
- **Tesseract native libs in Docker** — mitigated by `TesseractOCR` + apt deps.
- **Bleeding-edge versions** (.NET 10 / Angular 22 / Signal Forms): pin exact
  versions at scaffold time and verify the `TesseractOCR` package targets .NET 10.
