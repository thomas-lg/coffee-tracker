# Coffee Tracker

[![CI](https://github.com/thomas-lg/coffee-tracker/actions/workflows/ci.yml/badge.svg)](https://github.com/thomas-lg/coffee-tracker/actions/workflows/ci.yml)

A self-hosted web app to catalog the coffees I buy and rate them to taste, with
multiple users each keeping their own ratings. Built to be shared — anyone can
self-host it on a NAS via Docker.

This is a **personal, for-fun project** — a deliberately chill, no-pressure space
to learn modern C#/.NET (and enjoy good coffee) on my own schedule. No roadmap
commitments, no SLAs, no deadlines.

## Stack
- **Backend:** ASP.NET Core Web API (.NET 10), EF Core + SQLite (WAL mode),
  ASP.NET Core Identity + JWT.
- **Frontend:** Angular 22 (standalone components, signals, Signal Forms),
  shipped as an installable PWA.
- **Snap-to-fill:** photograph a coffee bag → open-source OCR (Tesseract first,
  behind a swappable `IOcrService`) pre-fills the Add Coffee form.
- **Deploy:** GitHub Actions builds a `linux/amd64` image and publishes it to
  GHCR; you install/update it manually from your NAS's Docker GUI.

See [PLAN.md](./PLAN.md) for the full design and build milestones (M0–M8).

## Getting started (Dev Container)

Development happens inside a dev container, so the only host prerequisites are
**Docker** and the **VS Code Dev Containers** extension.

1. Clone the repo and open it in VS Code.
2. Run **Dev Containers: Reopen in Container**. The first build installs the .NET
   10 SDK, Node 22, the Angular CLI, `dotnet-ef`, and the native Tesseract OCR
   libraries, then prints a toolchain summary.
3. The API runs HTTP-only on `http://localhost:5000` and the Angular dev server on
   `http://localhost:4200` (both forwarded automatically). `http://localhost` is a
   secure context, so the PWA service worker and camera work without HTTPS in dev.

## Install on Unraid (or any Docker host)

The published image is **public** at `ghcr.io/thomas-lg/coffee-tracker`. On Unraid,
add the container from [`deploy/unraid/my-coffee-tracker.xml`](./deploy/unraid/my-coffee-tracker.xml)
(or fill in the values below by hand), map the volumes, set the required env vars,
and **put it behind a reverse proxy that terminates HTTPS** (NPM, SWAG, Traefik,
Caddy) — the PWA and camera require a secure context. Don't expose the container
port directly to the internet.

### Volumes

| Container path | Purpose                          |
| -------------- | -------------------------------- |
| `/config`      | SQLite database (`coffee.db`)    |
| `/photos`      | Uploaded coffee photos           |

> Back up `/config` and `/photos` **before pulling a new image** — startup runs EF
> migrations automatically and there is no rollback. The database uses WAL mode, so
> `/config` holds three files (`coffee.db`, `coffee.db-wal`, `coffee.db-shm`); for a
> consistent single-file backup run `sqlite3 coffee.db ".backup backup.db"`.

> **Permissions:** the container starts as root, `chown`s `/config` and `/photos` to
> `PUID:PGID` on startup, then drops to that user. Defaults are `99:100` (Unraid's
> `nobody:users`). If you see `SQLite Error 14: unable to open database file`, set
> `PUID`/`PGID` to match whoever owns your host volume directories.

### Environment variables

| Variable                          | Required | Default          | Description                                                                 |
| --------------------------------- | -------- | ---------------- | --------------------------------------------------------------------------- |
| `Jwt__Key`                        | **yes**  | —                | Long random secret for signing auth tokens. The app refuses to start without a strong value (`openssl rand -base64 48`). |
| `Jwt__Issuer`                     | no       | `coffee-tracker` | JWT issuer claim.                                                           |
| `Jwt__Audience`                   | no       | `coffee-tracker` | JWT audience claim.                                                         |
| `Jwt__AccessTokenMinutes`         | no       | `15`             | Access-token lifetime (minutes). Kept short; sessions persist via a rotating refresh token, so a stolen access token expires quickly. |
| `Jwt__RefreshTokenDays`           | no       | `14`             | Refresh-token lifetime (days) — the effective session length. Refresh tokens rotate on use and are revoked on logout. |
| `Storage__SignedUrlLifetimeMinutes` | no     | `60`             | How long a signed `/photos/…` URL stays valid (minutes). Photos are served only via short-lived signed URLs, never anonymously. |
| `REGISTRATION_ENABLED`            | no       | `false`          | When `false`, new signups are blocked (safe default for a public instance). Set `true` to allow registration; the first user becomes admin. |
| `ForwardedHeaders__KnownProxies`  | recommended | —             | Comma-separated IP(s) of your reverse proxy (SWAG/Authelia), so the app trusts `X-Forwarded-For`/`-Proto`. **Set this** behind a proxy — otherwise auth rate-limiting keys off the proxy's single IP and throttles all clients together. |
| `Ocr__Engine`                     | no       | `tesseract`      | OCR engine for `/api/coffees/scan`: `tesseract` (uses the bundled native libs) or `none` (disables scanning → 503). |
| `Ocr__TessdataPath`               | no       | system path      | Override the tessdata directory; defaults to the `TESSDATA_PREFIX` system path (the image ships English data). |
| `Ocr__Language`                   | no       | `eng`            | Tesseract language code. |
| `Ocr__TimeoutSeconds`             | no       | `30`             | Hard ceiling on a single OCR run; a slower/stuck scan is terminated and returns `503` so it can't pin a worker. |
| `Ocr__MaxConcurrency`             | no       | `0` (≈ 2× CPUs)  | Max OCR processes running at once; extra scans queue instead of spawning unbounded `tesseract` processes. `0` resolves to twice the processor count. |
| `PUID`                            | no       | `99`             | User ID the app runs as. Set to match your host volume owner so `/config`/`/photos` are writable (Unraid default `99` = `nobody`). |
| `PGID`                            | no       | `100`            | Group ID the app runs as (Unraid default `100` = `users`). |

## Updating

Pull the new `:latest` (or a pinned `:sha-…` / `:vX.Y.Z`) tag in your Docker GUI
and recreate the container. Volumes persist your data across the update.

## Security notes

This app is designed to be internet-exposed and shared, so: no secrets are baked
into the image (all injected at runtime), there is no default JWT key, registration
is gated by a flag, login/register are rate-limited, and the container runs as a
non-root user. Auth uses **short-lived access tokens plus rotating, revocable refresh
tokens** (reuse of a rotated token revokes the whole session family). Uploaded photos
are **re-encoded** on upload (stripping any embedded payload/metadata) and served only
through **short-lived signed URLs** — never anonymously. See the Security section in
[PLAN.md](./PLAN.md).

## Status

All planned milestones (**M0–M8**) are shipped and merged:

- ✅ **M0** — dev container (reproducible .NET 10 + Node 22 + Tesseract toolchain)
- ✅ **M1** — backend skeleton over EF Core + SQLite (WAL, auto-migrate),
  **hexagonal architecture** (Domain ← Application ← {Infrastructure, Api})
- ✅ **M2** — coffee CRUD + photo upload behind an `IPhotoStorage` port
  (content-type allowlist, 5 MB cap, server-generated names), served at `/photos`
- ✅ **M3** — auth: ASP.NET Identity + JWT, `REGISTRATION_ENABLED` gate (first user
  is admin), password policy + lockout, rate-limited endpoints, `Jwt__Key` required
- ✅ **M4** — reviews, ratings & flavor tags, with `averageRating`/`reviewCount`
- ✅ **M5** — snap-to-fill OCR (backend): `POST /api/coffees/scan` over a swappable
  `IOcrService` + a pure `CoffeeLabelParser`
- ✅ **M6** — Angular 22 PWA: auth, catalog (search + ratings-over-time), add/edit,
  snap-to-fill UX, installable + light/dark theming
- ✅ **M7** — production multi-stage Docker image + local `docker-compose.yml`
- ✅ **M8** — CI/CD → GHCR (`latest`/`sha`/semver) + the Unraid template

**Beyond the plan:** ratings **over time** (multiple dated reviews per coffee, not
one-per-user), **admin photo-cleanup** (reap scan-orphaned photos — backend + UI),
a **non-root PUID/PGID** container for Unraid bind mounts, **CodeQL + Trivy** image
scanning, **Dependabot**, build-provenance attestations, and an HTTP **integration
test** for the admin authorization policy.

## Ideas for later

Nothing committed — a parking lot for when the mood strikes:

- **OIDC / SSO** via Authelia (keep the app's own login as a fallback).
- **Brew log** — per-cup extraction notes (grind, dose, yield, time) beyond a rating.
- **Wishlist & "finished bag"** states; optional low-stock nudges.
- **Stats & charts** — rating trends over time, favourite roasters/origins.
- **Export / import** (JSON/CSV) and a one-click backup endpoint.
- **OCR upgrade** — PaddleOCR/RapidOCR behind `IOcrService` if Tesseract is weak on
  real bags.
- **Multi-arch image** (add `linux/arm64`) for ARM NAS / Raspberry Pi.
- **i18n** — the UI is English-only today.

## Built with Claude

I use [Claude](https://claude.ai) (via Claude Code) to help design and build this
project.
