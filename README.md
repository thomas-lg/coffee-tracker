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
add the container from [`deploy/unraid/coffee-tracker.xml`](./deploy/unraid/coffee-tracker.xml)
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

### Environment variables

| Variable                          | Required | Default          | Description                                                                 |
| --------------------------------- | -------- | ---------------- | --------------------------------------------------------------------------- |
| `Jwt__Key`                        | **yes**  | —                | Long random secret for signing auth tokens. The app refuses to start without a strong value (`openssl rand -base64 48`). |
| `Jwt__Issuer`                     | no       | `coffee-tracker` | JWT issuer claim.                                                           |
| `Jwt__Audience`                   | no       | `coffee-tracker` | JWT audience claim.                                                         |
| `REGISTRATION_ENABLED`            | no       | `false`          | When `false`, new signups are blocked (safe default for a public instance). Set `true` to allow registration; the first user becomes admin. |
| `ForwardedHeaders__KnownProxies`  | recommended | —             | Comma-separated IP(s) of your reverse proxy (SWAG/Authelia), so the app trusts `X-Forwarded-For`/`-Proto`. **Set this** behind a proxy — otherwise auth rate-limiting keys off the proxy's single IP and throttles all clients together. |

## Updating

Pull the new `:latest` (or a pinned `:sha-…` / `:vX.Y.Z`) tag in your Docker GUI
and recreate the container. Volumes persist your data across the update.

## Security notes

This app is designed to be internet-exposed and shared, so: no secrets are baked
into the image (all injected at runtime), there is no default JWT key, registration
is gated by a flag, login/register are rate-limited, uploads are validated, and the
container runs as a non-root user. See the Security section in [PLAN.md](./PLAN.md).

## Status
Early days, but the backend foundation is now running:

- ✅ **M0** — dev container (reproducible .NET 10 + Node 22 + Tesseract toolchain)
- ✅ **Scaffolding & CI/CD** — `ci.yml` (build/test) + `release.yml` (GHCR publish),
  solution + xUnit test skeleton, `.dockerignore`, Unraid template
- ✅ **M1** — backend skeleton: `GET /api/coffees` over EF Core + SQLite (WAL mode,
  auto-migrate on startup), Swagger in dev, **hexagonal architecture** (Domain ←
  Application ← {Infrastructure, Api}) so controllers depend only on application
  ports, never on EF Core
- ✅ **M2** — coffee CRUD (`GET`/`POST`/`PUT`/`DELETE /api/coffees`) with
  DataAnnotations validation, plus photo upload (`POST /api/coffees/{id}/photo`)
  behind an `IPhotoStorage` port — content-type allowlist, 5 MB cap, server-generated
  filenames — served read-only at `/photos`
- ✅ **M3** — auth: ASP.NET Identity + JWT (`POST /api/auth/register` & `/login`),
  `REGISTRATION_ENABLED` gate (first user becomes admin), password policy + lockout,
  rate-limited auth endpoints, and **all** catalog endpoints now require a token.
  `Jwt__Key` is required at startup (no baked default); created coffees record their owner
- ✅ **M4** — reviews & ratings: one review per user per coffee (rating 1–5, tasting
  notes, brew details, flavor tags), owner-only edit / owner-or-admin delete, a seeded
  `GET /api/flavor-tags` set, and `averageRating`/`reviewCount` on every coffee response
- ⬜ **M5–M6** — OCR snap-to-fill, Angular 22 PWA
- ⬜ **M7** — production Docker image + local compose

The CI/CD workflows are intentionally scaffolding-aware: the frontend,
docker-build, and image-publish steps are no-ops until their code lands (M6/M7).

## Built with Claude

I use [Claude](https://claude.ai) (via Claude Code) to help design and build this
project.
