# Coffee Tracker — Self-Hosted Web App

## Context

You want a self-hosted web app (running on your NAS via Docker) to catalog the
coffees you buy and rate them to taste, with **multiple users** each keeping
their own ratings. You're comfortable with **Angular** and want to **learn
modern C# / .NET** along the way — so this project doubles as a learning vehicle.
The plan favors current, idiomatic tech and explains the *why* behind the .NET
concepts as we build.

### Confirmed decisions
- **Backend:** ASP.NET Core Web API on **.NET 10** (current LTS), **EF Core** +
  **SQLite**, **ASP.NET Core Identity + JWT** for auth.
- **Frontend:** **Angular 22** (standalone components, signals, the new stable
  Signal Forms), shipped as an **installable PWA** (home-screen icon, camera).
- **Auth:** open registration + login; **first registered user becomes admin**.
- **Tracked fields:** core details, per-user rating & tasting notes, brew
  details, and where it was bought.
- **Snap-to-fill (OCR):** photograph a coffee bag on your phone → server runs
  **open-source OCR** → the Add Coffee form is **pre-filled** for you to confirm.
  Engine sits behind an **`IOcrService`** interface; **Tesseract** is the first
  implementation (in-process, offline, lightweight), swappable for
  PaddleOCR/RapidOCR later if bag accuracy disappoints.
- **Deploy:** **Docker Compose**, a single image (API serves the built Angular
  app), with volumes for the SQLite DB and uploaded photos.
- **Learning depth:** guided & well-commented (concept notes in code + plan).

---

## Architecture overview

```
coffee-tracker/
├─ backend/
│  └─ CoffeeTracker.Api/        # ASP.NET Core Web API (.NET 10)
│     ├─ Program.cs             # app wiring: DI, auth, EF, routing, static files
│     ├─ Models/                # EF Core entities (AppUser, Coffee, Review, FlavorTag)
│     ├─ Data/                  # AppDbContext + migrations + DB seeding
│     ├─ Dtos/                  # request/response shapes (kept separate from entities)
│     ├─ Endpoints/ (or Controllers/)  # API routes grouped by feature
│     ├─ Services/              # token svc, photo storage, IOcrService + TesseractOcr
│     └─ appsettings.json       # config (JWT key, connection string, OCR engine)
├─ frontend/                    # Angular 22 app (PWA)
│  └─ src/app/
│     ├─ core/                  # auth service, JWT interceptor, guards, models
│     ├─ features/coffees/      # list, detail, add/edit, snap-to-fill capture
│     ├─ features/auth/         # login, register
│     └─ shared/                # rating stars, tag chips, camera capture, etc.
├─ Dockerfile                   # multi-stage: build Angular -> build API -> runtime
├─ docker-compose.yml           # 1 service + volumes (db, photos)
└─ README.md                    # setup + NAS deploy notes
```

**Why this shape:** the Angular app is built into static files and served by the
ASP.NET Core app, so production is a single container (simplest to run on a NAS).
In development we run them separately (`dotnet watch` + `ng serve`) with CORS so
each has fast hot-reload.

---

## Data model

Two ideas kept separate so multiple users can rate the *same* coffee:

- **Coffee** (shared catalog entry — one per bean/bag):
  `Id, Name, Roaster, Origin, RoastLevel, Price, DateBought, PhotoPath,
  ShopName, PurchaseUrl, CreatedByUserId, CreatedAt`
- **Review** (one per user per coffee — this is "rate it to your taste"):
  `Id, CoffeeId, UserId, Rating (1–5), TastingNotes, BrewMethod, Grind, Ratio,
  CreatedAt, UpdatedAt`
- **FlavorTag** + **ReviewTag** (many-to-many: fruity, chocolatey, nutty…) —
  *a clean place to learn EF Core relationships*. (Fallback: a comma-separated
  string on Review if we want to keep v1 tiny.)
- **AppUser** : extends `IdentityUser` (adds `IsAdmin`, `DisplayName`).

The coffee detail view shows the **average rating** across all users plus each
person's individual review.

---

## Build milestones (we'll do these together, incrementally)

Each milestone is independently runnable so you can see/learn as we go.

### M1 — Backend skeleton + database
- `dotnet new webapi` (.NET 10), add EF Core + SQLite packages.
- Define `Coffee` entity + `AppDbContext`; create the first **migration** and
  apply it (file-based SQLite DB). *Learn: EF Core code-first, migrations, DI.*
- One read endpoint (`GET /api/coffees`) + Swagger/OpenAPI to click around.

### M2 — Coffee CRUD
- Full CRUD endpoints for coffees with **DTOs** + validation.
- **Photo upload** endpoint storing files in the photos volume; serve them back.
- *Learn: model binding, validation, file handling, async/await over EF.*

### M3 — Auth (Identity + JWT)
- Add ASP.NET Core Identity over `AppUser`; register/login endpoints issuing JWT.
- First registered user flagged admin; protect write endpoints with `[Authorize]`.
- *Learn: password hashing, claims, JWT, authorization policies.*

### M4 — Reviews & ratings
- `Review` entity + endpoints (create/update your own review, list reviews for a
  coffee, compute average). Enforce "one review per user per coffee."
- Add `FlavorTag` many-to-many. *Learn: relationships, ownership checks.*

### M5 — Snap-to-fill OCR (backend)
- Define **`IOcrService`** (`Task<OcrResult> ReadAsync(Stream image)`), then a
  **`TesseractOcr`** implementation using the `Tesseract` NuGet (libtesseract,
  in-process, offline). Bundle the English `tessdata` in the image.
- `POST /api/coffees/scan` → accepts a photo, returns extracted raw text + a
  **best-effort parse** into fields (name/roaster/origin/roast/weight via
  heuristics + regex). Engine selected via `appsettings.json` so PaddleOCR/
  RapidOCR can be added later without touching callers.
- *Learn: interfaces + DI for swappable implementations, working with image
  streams, options/config binding.*

### M6 — Angular frontend (PWA) + snap-to-fill UX
- Angular 22 app: standalone components + **signals** for state, **Signal Forms**
  for the add/edit + login forms; configured as an **installable PWA**
  (`@angular/pwa`: manifest, service worker, app icon, offline shell).
- `AuthService` + **JWT HTTP interceptor** + route **guards**.
- Pages: login/register, coffee list (search + avg rating), coffee detail (your
  review + everyone's), add/edit coffee, photo display.
- **Snap-to-fill flow:** a camera-capture control
  (`<input type="file" accept="image/*" capture="environment">`) → upload to
  `/api/coffees/scan` → pre-fill the form from the response → you review/edit →
  save. The original photo is kept as the coffee's image.

### M7 — Dockerize + NAS deploy
- Multi-stage `Dockerfile` (build Angular → publish API → slim runtime image)
  that also installs the Tesseract native lib + `tessdata`.
- `docker-compose.yml` with named volumes for `coffee.db` and `/photos`, and the
  JWT signing key via env/secret.
- README with steps for your NAS (Synology/QNAP/Unraid all run Compose), incl.
  HTTPS note (PWA + camera need a secure context — typically via your NAS's
  existing reverse proxy / Let's Encrypt cert).

---

## Key files to create

- `backend/CoffeeTracker.Api/Program.cs` — central wiring (DI, EF, Identity/JWT,
  static-file serving of the Angular build, CORS for dev).
- `backend/CoffeeTracker.Api/Data/AppDbContext.cs` — DbContext + entity config.
- `backend/CoffeeTracker.Api/Models/*.cs` — `Coffee`, `Review`, `FlavorTag`, `AppUser`.
- `backend/CoffeeTracker.Api/Endpoints/*.cs` — coffees (incl. `/scan`), reviews, auth.
- `backend/CoffeeTracker.Api/Services/` — `TokenService.cs`, `PhotoStorage.cs`,
  `IOcrService.cs`, `TesseractOcr.cs`, `CoffeeLabelParser.cs` (text → fields).
- `frontend/src/app/core/*` — `auth.service.ts`, `auth.interceptor.ts`, `auth.guard.ts`, models.
- `frontend/src/app/features/*` — coffees (+ snap-to-fill capture) + auth components.
- `frontend/` PWA assets — `manifest.webmanifest`, service worker, icons.
- `Dockerfile`, `docker-compose.yml`, `README.md`.

> No existing code to reuse — this is a greenfield app in a new
> `C:\Users\tomle\workspace\coffee-tracker` folder. Your `plex-releases-summary`
> project is the reference only for tooling style (Docker Compose, volumes, env
> config, a `scripts/` helper folder).

---

## Verification

- **Per milestone, run locally:**
  - Backend: `dotnet watch run` → open Swagger UI, exercise endpoints.
  - Frontend: `ng serve` → http://localhost:4200, talking to the API.
- **Auth check:** register two users; confirm user 1 is admin, each sees/edits
  only their own review, and unauthenticated writes are rejected (401).
- **Data check:** add a coffee with a photo, add reviews from both users, confirm
  the average rating and per-user reviews render correctly.
- **Snap-to-fill check:** from a phone (or `capture` in browser dev tools), upload
  a coffee-bag photo to `/api/coffees/scan`; confirm text is extracted and the
  form pre-fills. Try a few real bags to gauge Tesseract accuracy — if weak,
  this is the point where we'd swap in PaddleOCR/RapidOCR via `IOcrService`.
- **PWA check:** load over HTTPS on your phone, "Add to Home Screen", launch
  fullscreen, and confirm the camera opens for snap-to-fill.
- **End-to-end container:** `docker compose up --build`, hit the single
  container, confirm data + photos persist across `docker compose down/up`
  (volumes working).

---

## Open choices we can settle as we build (sensible defaults chosen, easy to change)
- **Endpoints style:** minimal APIs (modern .NET default) vs controllers — I'll
  start with controllers for readability while learning, easy to switch.
- **Flavor tags:** full many-to-many (more to learn) vs simple string (smaller).
- **OCR engine:** starting with Tesseract; `IOcrService` lets us swap to
  PaddleOCR/RapidOCR if real coffee-bag accuracy isn't good enough.
- **HTTPS / reverse proxy on the NAS:** PWA + camera require a secure context;
  we'll terminate TLS at your NAS's existing reverse proxy and note it in README.
