# Next steps & handoff

Working notes to resume on another machine. Backend is complete through the
plan's API milestones; what's left is one small backend follow-up, the frontend,
and the production image.

## Where things stand (as of M5 merge)

- **Done on `main`:** M0, scaffolding/CI-CD, **M1** (catalog + hexagonal + SQLite WAL),
  **M2** (CRUD + photo upload), **M3** (auth: Identity + JWT, hardened), **M4**
  (reviews/ratings/flavor-tags + moderation), **M5** (snap-to-fill OCR backend).
- **OpenSpec specs (live):** `auth`, `coffee-catalog`, `label-scan`, `photo-storage`,
  `reviews`. No active changes; `openspec validate --specs` is green.
- **Workflow:** OpenSpec change per milestone → feature branch → PR → CI green →
  squash-merge → separate PR archiving the change into `openspec/specs/`.

## Proposed order

### 1. Admin photo-cleanup (small, backend-only) — do first
Closes the scan-orphan gap from the M5 review (we keep storing the scan photo, but
unused ones need cleanup). Agreed design:
- `IPhotoStorage.ListAsync()` → stored photo relative paths.
- A service that diffs stored photos against `Coffee.PhotoPath` to mark **used /
  unused**.
- `GET /api/admin/photos` → list with the used flag; `DELETE /api/admin/photos` →
  **delete by selected paths** (admin reviews first — not "delete all unused").
- **Admin-only** (`IsAdmin` claim) + `[Authorize]`. The bulk-select **table UI is
  M6**.
- Start with an OpenSpec change (extend `photo-storage` or a new `photo-admin`
  capability).

### 2. M6 — Angular 22 PWA (big, frontend) — see PLAN.md "M6"
- `ng new` (Angular 22, standalone, signals) in `frontend/`; add `@angular/pwa`.
- `core/`: `auth.service` (signals + localStorage token), `auth.interceptor`
  (attach JWT), `auth.guard`, typed models.
- `features/auth` (login/register via Signal Forms; hide register when
  `REGISTRATION_ENABLED` is off — needs a small config endpoint), `features/coffees`
  (list w/ search + avg rating, detail w/ reviews, add/edit Signal Forms, photo).
- **Snap-to-fill UX:** `<input type="file" accept="image/*" capture="environment">`
  → `POST /api/coffees/scan` → pre-fill the add form → user edits → save (reuse the
  returned `photoPath` as the coffee image).
- Admin **photo-cleanup table** (from step 1): used/unused list + bulk-select delete.
- Dev: `proxy.conf.json` so `ng serve` (:4200) talks to the API; add matching CORS
  in the API **dev** config only.
- CI cleanup: drop the `frontend/package.json` no-op guard now that `frontend/` exists.

### 3. M7 — production image + compose — see PLAN.md "M7"
- Multi-stage root `Dockerfile`: node build Angular → dotnet publish API → ASP.NET
  runtime on Ubuntu Noble with `apt-get install tesseract-ocr tesseract-ocr-eng
  libtesseract-dev libleptonica-dev`, `TESSDATA_PREFIX=/usr/share/tesseract-ocr/5`,
  serve Angular `dist` same-origin, `ASPNETCORE_URLS=http://+:8080`, non-root, only
  `/config`+`/photos` writable.
- `Program.cs` prod wiring: `UseForwardedHeaders` (already added in M3 for rate
  limiting — extend KnownProxies) + **HSTS**.
- `docker-compose.yml` (local/reference only), `.dockerignore`, CI drop the
  Dockerfile no-op guard. **This is where the real Tesseract OCR path runs.**

## Context / gotchas to remember

- **Local smoke tests: do NOT use port 5000** — macOS AirPlay/ControlCenter squats
  it and stale backgrounded `dotnet run` processes serve old binaries (cost real
  debugging time). Run with `ASPNETCORE_URLS=http://localhost:5099 dotnet run
  --no-launch-profile --no-build`; suspect the port/stale process before the code.
- **OCR on the host:** `Ocr:Engine=none` in `appsettings.Development.json` so the
  host app runs without native libs; `/api/coffees/scan` returns **503** there. Real
  OCR runs in the dev container / prod image (M7). To test locally: `brew install
  tesseract` + `Ocr__Engine=tesseract`.
- **Commit email** for this repo is `tom.legougaud@gmail.com` (not the work email).
- **Deployment:** NAS, internet-exposed via SWAG + Authelia. The app keeps its **own**
  login (all endpoints require a token, reads included). Future option: Authelia
  SSO/OIDC. Set `ForwardedHeaders__KnownProxies` behind the proxy or auth
  rate-limiting keys off the proxy's single IP.
- **Conventions:** hexagonal (controllers depend only on application ports, never EF);
  DTOs at the boundary (entities never serialized); JWT signing key from env only
  (no baked default; app fails fast); concept-note comments inline.
