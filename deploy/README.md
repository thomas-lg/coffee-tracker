# Deployment & operations runbook

How Coffee Tracker ships and runs in production. The model is **GitHub Actions →
GHCR (public image) → manual install via the Unraid Docker GUI** — no Watchtower,
SSH, or compose-on-NAS. Images are `linux/amd64` only.

Canonical image: `ghcr.io/thomas-lg/coffee-tracker` (tags: `latest`, `sha-<short>`,
and `vX.Y.Z` on version tags).

---

## 1. Make the GHCR package public (one-time, GitHub UI)

So the NAS can pull without authenticating. There is no REST API for this — do it in
the web UI:

1. Go to the package: **https://github.com/users/thomas-lg/packages/container/package/coffee-tracker**
2. **Package settings** → **Danger Zone** → **Change visibility** → **Public**.

Verify from any Docker host (no login):

```bash
docker pull ghcr.io/thomas-lg/coffee-tracker:latest
```

---

## 2. Branch protection on `main` (applied)

Configured via the API to match `PLAN.md`. Current ruleset:

- Pull request required before merge (**0** required approvals — solo maintainer).
- Required status checks, **up to date**: `backend`, `frontend`, `docker-build`.
- **Linear history** (squash- or rebase-merge only — no merge commits).
- Force-pushes and branch deletion **blocked**.
- **Not enforced for admins** (the maintainer self-merges).

View or edit at **Settings → Branches → Branch protection rules**, or via:

```bash
gh api repos/thomas-lg/coffee-tracker/branches/main/protection   # if gh is installed
```

> Requires a public repo or GitHub Pro to stay enabled (this repo is public).

---

## 3. Install on Unraid (manual, per host)

The app keeps its **own** login (every endpoint needs a token); still put it behind a
TLS-terminating reverse proxy (SWAG/Authelia/NPM/Traefik) — the PWA and camera need a
secure context. Don't publish the container port directly to the internet.

1. Copy [`unraid/my-coffee-tracker.xml`](./unraid/my-coffee-tracker.xml) to
   `/boot/config/plugins/dockerMan/templates-user/my-coffee-tracker.xml` on the NAS.
2. **Add Container** → Template: **User templates** → `coffee-tracker`.
3. Map the volumes and set the env vars:
   - `/config` → e.g. `/mnt/user/appdata/coffee-tracker/config` (SQLite DB)
   - `/photos` → e.g. `/mnt/user/appdata/coffee-tracker/photos` (uploads)
   - **`Jwt__Key`** (required) — `openssl rand -base64 48`
   - **`ForwardedHeaders__KnownProxies`** — your reverse proxy's IP
   - **`PUID`/`PGID`** — match the host owner of the appdata dirs (Unraid default
     `99`/`100`); the container chowns the volumes to this on start and drops to it.
4. **Bootstrap the admin:** set `REGISTRATION_ENABLED=true`, start, register (the
   first user becomes admin), then set it back to `false` and recreate.
5. Point the reverse proxy at the container's `8080`.

### Behind SWAG + Authelia

The app runs fine behind a forward-auth gateway, but two things are worth knowing.

**Bypass Authelia for `/api`.** Every API endpoint already requires the app's own JWT
(plus rate-limited login), so gate only the pages with Authelia and let the API defend
itself — the same model the sonarr/radarr sample confs use for their API keys:

```yaml
access_control:
  rules:
    - domain: coffee.example.com
      resources: ['^/api/.*']
      policy: bypass
```

Keep everything else (including `/photos`, which has **no** app-level auth) behind
Authelia. Without the bypass, an expired Authelia session makes every API call fail
with a gateway 401 while the page still renders — the app detects that (a 401 without
the API's `WWW-Authenticate: Bearer` challenge) and does one full-page reload so
Authelia can re-authenticate the browser.

**Why this app behaves differently from non-PWA containers.** The service worker
serves the app shell from the browser cache, so opening/refreshing the app doesn't
necessarily hit the proxy, and `fetch()` calls can never redirect the window — Authelia
answers them 401, not 302. The service worker uses network-first navigations
(`navigationRequestStrategy: freshness` in `ngsw-config.json`) so that refreshes do
reach the proxy and get the usual redirect to the Authelia portal; don't remove that
setting if the deployment sits behind forward auth.

### Updating

Pull a newer tag (`:latest` or a pinned `:sha-…` / `:vX.Y.Z`) and recreate the
container. **Back up `/config` + `/photos` first** — startup auto-applies EF
migrations with no rollback. In WAL mode `/config` holds three files; for a
consistent single-file snapshot use `sqlite3 coffee.db ".backup backup.db"`.

---

See the repo [README](../README.md) for the full environment-variable reference and
[PLAN.md](../PLAN.md) for the design rationale.
