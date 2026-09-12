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
   10 SDK, Node 24, the Angular CLI, `dotnet-ef`, and the native Tesseract OCR
   libraries, then prints a toolchain summary.
3. The API runs HTTP-only on `http://localhost:5000` and the Angular dev server on
   `http://localhost:4200` (both forwarded automatically). `http://localhost` is a
   secure context, so the PWA service worker and camera work without HTTPS in dev.

### Backend dependency bumps

`backend/Directory.Build.props` enables NuGet lock files and CI restores with
`--locked-mode`, so a bump can't land without a reviewed `packages.lock.json`. NuGet keeps
one lock file per project, so changing a package in `Application` or `Infrastructure` also
invalidates the ones in `Api` and `Tests` and the restore fails with **NU1004**. Refresh
them all with:

```powershell
./scripts/refresh-lockfiles.ps1          # rewrites the lock files (--force-evaluate)
./scripts/refresh-lockfiles.ps1 -Check   # just reproduce the CI restore (--locked-mode)
```

It uses a local .NET SDK if you have one and the pinned SDK container otherwise, so it
works on a bare host too.

Dependabot hits this on every backend PR. To refresh one without a local toolchain,
`gh workflow run refresh-lockfiles.yml -f pr=<number>` does the restore and pushes the
lock files — but it cannot make the checks pass on its own: GitHub parks any run triggered
by `github-actions[bot]` as `action_required`, and `GITHUB_TOKEN` can neither start nor
approve its own runs. The job summary says so and prints the one command that finishes it:

```bash
gh pr close <number> && gh pr reopen <number>
```

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
| `Oidc__Authority`                 | no       | —                | Base URL of an OpenID Connect provider (Authelia, Keycloak, Authentik, Google…). Set it with `Oidc__ClientId` to offer sign-in through that provider; leave both unset to run with app accounts only. Endpoints are discovered from `/.well-known/openid-configuration`. |
| `Oidc__ClientId`                  | no       | —                | The client id registered with that provider. Required whenever `Oidc__Authority` is set — the app refuses to start with only one of the two. |
| `Oidc__Scopes`                    | no       | `openid profile email` | Scopes requested from the provider. Add `groups` if you use the admin claim mapping below. |
| `Oidc__DisplayName`               | no       | —                | What to call the provider on the sign-in button (e.g. `Authelia`). Unset, the button reads "your identity provider" — the app never hard-codes which product it talks to. |
| `Oidc__AdminClaim`                | no       | —                | Claim carrying the admin assertion (e.g. `groups`). Set with `Oidc__AdminClaimValue`; both or neither. Unset, the first user to sign in through the provider becomes admin. |
| `Oidc__AdminClaimValue`           | no       | —                | Value `Oidc__AdminClaim` must carry to grant admin. Re-evaluated on every sign-in, so removing someone from the group revokes their rights at their next sign-in. |
| `REGISTRATION_ENABLED`            | legacy   | —                | **No longer needed.** A fresh instance accepts its first account with nothing configured, then closes registration by itself; afterwards both settings live in **Admin → Account settings**. On an instance that already had users when it was upgraded, this variable is read once to preserve its registration posture, and ignored from then on. |
| `ForwardedHeaders__KnownProxies`  | recommended | —             | Comma-separated **host names or IPs** of your reverse proxy, so the app trusts its `X-Forwarded-For`/`-Proto`. **Set this** behind a proxy — otherwise auth rate-limiting keys off the proxy's single IP and throttles every client together, and HSTS is not emitted. Prefer a name (`swag`, a service name, a DNS record) over an address: an orchestrator assigns the address, so a pinned IP holds only until the proxy restarts onto another one. Names are resolved at startup; one that cannot be resolved is ignored rather than guessed. |
| `Ocr__Engine`                     | no       | `tesseract`      | OCR engine for `/api/coffees/scan`: `tesseract` (uses the bundled native libs) or `none` (disables scanning → 503). |
| `Ocr__TessdataPath`               | no       | system path      | Override the tessdata directory; defaults to the `TESSDATA_PREFIX` system path (the image ships English data). |
| `Ocr__Language`                   | no       | `eng`            | Tesseract language code. |
| `Ocr__TimeoutSeconds`             | no       | `30`             | Hard ceiling on a single OCR run; a slower/stuck scan is terminated and returns `503` so it can't pin a worker. |
| `Ocr__MaxConcurrency`             | no       | `0` (≈ 2× CPUs)  | Max OCR processes running at once; extra scans queue instead of spawning unbounded `tesseract` processes. `0` resolves to twice the processor count. |
| `PUID`                            | no       | `99`             | User ID the app runs as. Set to match your host volume owner so `/config`/`/photos` are writable (Unraid default `99` = `nobody`). |
| `PGID`                            | no       | `100`            | Group ID the app runs as (Unraid default `100` = `users`). |

## Signing in

Two ways in, and an instance can offer either or both.

**App accounts** work out of the box. A brand-new instance accepts registrations until
its first account exists — that account becomes the administrator — and then closes
registration by itself, so an instance left on the internet is never sitting open by
accident. An administrator reopens it from **Admin → Account settings** to add people,
and registration reopened deliberately stays open until it is turned off again.

**An identity provider**, if you run one. Set `Oidc__Authority` and `Oidc__ClientId` to
any OpenID Connect provider and the sign-in screen gains a provider button. Register the
app as a *public* client using the authorization code flow with PKCE, with your app's
root URL as the redirect URI. Nothing in the app is specific to any provider: everything
else comes from its discovery document.

Identities are matched on the provider's stable subject, so someone changing their email
keeps their coffees. A first sign-in whose email matches an existing app account is
linked to it only when the provider asserts the address is verified; otherwise the
sign-in is refused rather than quietly creating a second account.

**The provider is the guest list.** Anyone the provider lets through gets an account on
first sign-in, so point the app at a provider you control and that gates who may use it
(Authelia's `access_control`, a Keycloak client role, a Google Workspace domain…). A
provider that accepts the whole world — plain Google, for instance — makes the app accept
the whole world with it. Only an email the provider asserts as verified is recorded;
otherwise the account gets a placeholder address, so nobody can claim someone else's.

Once an administrator has signed in through the provider at least once, you can switch
app-account sign-in off entirely. Before that the app refuses to — it would be the last
way in.

**If the provider later disappears** (URL changed, certificate expired, instance gone)
and app-account sign-in is off, nobody can get in. Turn it back on directly in the
database, then restart the container:

```sh
sqlite3 /config/coffee.db "UPDATE AppSettings SET LocalLoginEnabled = 1;"
```

## Updating

Pull the new `:latest` (or a pinned `:sha-…` / `:vX.Y.Z`) tag in your Docker GUI
and recreate the container. Volumes persist your data across the update.

### Release channels

| Tag | Built from | Who gets it |
|---|---|---|
| `:latest` | `main`, once CI passes | The default. Nothing else moves it. |
| `:beta` | the `beta` branch, once CI passes | Only containers pinned to `:beta`. |
| `:vX.Y.Z` | a version tag | Pinned, immutable. |
| `:sha-…` | any published build | Pinned, immutable; pruned after 30 days. |

`beta` is for trying a change on real hardware before it reaches anyone. Merge into the
`beta` branch, wait for CI, and point a container at `:beta`. Nothing tracking `:latest`
sees it, and the two channels never share a build — a publish always builds the commit
whose CI passed, not whatever is on the default branch.

Going back is repointing the container at `:latest`. **Take a copy of `coffee.db`
first** if the beta carried a database migration: the schema change survives the
rollback, and while the old code tolerates a table it does not know about, restoring a
pre-migration file is the only way to truly undo one.

## Security notes

This app is designed to be internet-exposed and shared, so: no secrets are baked
into the image (all injected at runtime), there is no default JWT key, a fresh
instance closes registration behind its first account, login/register are
rate-limited — as are the anonymous config endpoint and label scanning, whose cost
a single caller could otherwise impose at will — and the container runs as a
non-root user. Auth uses **short-lived access tokens plus rotating, revocable
refresh tokens** (reuse of a rotated token revokes the whole session family).
Signing out revokes the refresh token immediately; an access token already issued
stays valid until it expires, which is why it is kept to 15 minutes. Uploaded
photos are **re-encoded** on upload (stripping any embedded payload/metadata) and
served only through **short-lived signed URLs** — never anonymously. Every response
carries a **content security policy** (no inline script), `X-Frame-Options: DENY`,
`Referrer-Policy: no-referrer` and `nosniff`, so an injected script — the shortest
path to the session, which lives in `localStorage` — has no way to run. See the
Security section in
[PLAN.md](./PLAN.md).

## Status

All planned milestones (**M0–M8**) are shipped and merged:

- ✅ **M0** — dev container (reproducible .NET 10 + Node 24 + Tesseract toolchain)
- ✅ **M1** — backend skeleton over EF Core + SQLite (WAL, auto-migrate),
  **hexagonal architecture** (Domain ← Application ← {Infrastructure, Api})
- ✅ **M2** — coffee CRUD + photo upload behind an `IPhotoStorage` port
  (content-type allowlist, 5 MB cap, server-generated names), served at `/photos`
- ✅ **M3** — auth: ASP.NET Identity + JWT, first user is admin, password policy +
  lockout, rate-limited endpoints, `Jwt__Key` required
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
