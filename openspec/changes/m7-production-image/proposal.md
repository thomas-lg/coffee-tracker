## Why

M0–M6 give a working app inside the dev container. M7 (in `PLAN.md`) packages it as
a **single production container** to self-host on Unraid: build the Angular PWA →
publish the API → one image that serves the SPA **same-origin** on `:8080`, with
OCR baked in and data on persistent volumes. Deploy stays manual (M8 publishes the
image to GHCR); this milestone produces the image + a local compose for testing.

## What Changes

- **Root multi-stage `Dockerfile`** (node build → `dotnet publish` → ASP.NET 10
  runtime on Ubuntu Noble with the same Tesseract package set as dev): copies the
  Angular `dist` into `wwwroot` (served same-origin), listens on `:8080`, runs as the
  image's non-root `app` user with only `/config` + `/photos` writable, sets
  `TESSDATA_PREFIX`, and defaults `ConnectionStrings__Default=Data Source=/config/coffee.db`
  + `Storage__PhotosPath=/photos`.
- **`Program.cs` prod wiring:** enable **HSTS** (TLS terminates at the NAS reverse
  proxy → no `HttpsRedirection`), serve static SPA files, and
  `MapFallbackToFile("index.html")` for Angular client routes — all **prod-only** so
  dev keeps Swagger at the root and `ng serve` separate. `UseForwardedHeaders`,
  migrate-on-startup, and SQLite WAL are already in place.
- **`docker-compose.yml`** — one service, named volumes for `/config` + `/photos`,
  documents the env (`Jwt__Key`, `REGISTRATION_ENABLED`). **Local dev/test +
  reference only**, explicitly not the NAS deploy path.
- **CI:** drop the `Dockerfile` existence guard in `ci.yml` (`docker-build`) and
  `release.yml` (`image`) so they build (and, for release, publish) for real.

Out of scope: the GHCR publish + Unraid install specifics (M8). The image build is
verified by **CI** (the dev container has no Docker, so it can't be built locally).

## Capabilities

### New Capabilities
- `deployment`: a single self-hosted production container — the SPA served
  same-origin with the API on `:8080`, OCR available, data persisted to `/config`
  + `/photos`, running non-root behind a TLS-terminating reverse proxy.

## Impact

- New root `Dockerfile` + `docker-compose.yml`; `Program.cs` prod pipeline;
  `.dockerignore` already present; CI/release guards removed.
- No database/schema change.
