# Coffee Tracker

[![CI](https://github.com/thomas-lg/coffee-tracker/actions/workflows/ci.yml/badge.svg)](https://github.com/thomas-lg/coffee-tracker/actions/workflows/ci.yml)

A self-hosted web app to catalog the coffees I buy and rate them to taste, with
multiple users each keeping their own ratings. Built to be shared — anyone can
self-host it on a NAS via Docker.

## Stack
- **Backend:** ASP.NET Core Web API (.NET 10), EF Core + SQLite, ASP.NET Core
  Identity + JWT.
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
> migrations automatically and there is no rollback.

### Environment variables

| Variable                          | Required | Default          | Description                                                                 |
| --------------------------------- | -------- | ---------------- | --------------------------------------------------------------------------- |
| `Jwt__Key`                        | **yes**  | —                | Long random secret for signing auth tokens. The app refuses to start without a strong value (`openssl rand -base64 48`). |
| `Jwt__Issuer`                     | no       | `coffee-tracker` | JWT issuer claim.                                                           |
| `Jwt__Audience`                   | no       | `coffee-tracker` | JWT audience claim.                                                         |
| `REGISTRATION_ENABLED`            | no       | `false`          | When `false`, new signups are blocked (safe default for a public instance). Set `true` to allow registration; the first user becomes admin. |
| `ForwardedHeaders__KnownProxies`  | no       | —                | IP(s) of your reverse proxy, so the app trusts `X-Forwarded-*` headers.     |

## Updating

Pull the new `:latest` (or a pinned `:sha-…` / `:vX.Y.Z`) tag in your Docker GUI
and recreate the container. Volumes persist your data across the update.

## Security notes

This app is designed to be internet-exposed and shared, so: no secrets are baked
into the image (all injected at runtime), there is no default JWT key, registration
is gated by a flag, login/register are rate-limited, uploads are validated, and the
container runs as a non-root user. See the Security section in [PLAN.md](./PLAN.md).

## Status
Planning. Application code (M1–M6) not started yet; the dev container, CI/CD
workflows, solution/test skeleton, and Unraid template are in place.
