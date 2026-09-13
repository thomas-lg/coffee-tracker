# Design notes

Standing rationale for Coffee Tracker — the decisions that shaped the app and still
govern it. For *what it does* and how to run it see [the README](../README.md); for
conventions when changing the code see [CLAUDE.md](../CLAUDE.md); for live behaviour
specs see [`openspec/specs/`](../openspec/specs/); for how each piece was built see the
archived changes in [`openspec/changes/archive/`](../openspec/changes/archive/).

## Context

A self-hosted coffee-cataloging web app, built as a vehicle to learn modern C#/.NET
while using familiar Angular. It is meant to be **shared publicly so others can
self-host it**, which is why security is a first-class requirement and every piece of
configuration is environment-driven.

Development happens **inside a dev container** — reproducible toolchain, no host setup.
Deployment is **GitHub Actions → GHCR (public image) → manual install from the NAS's
Docker GUI**; there is no Watchtower, SSH, or compose-on-NAS. The repo's
`docker-compose.yml` is a local dev/test and reference convenience, **not** the deploy
path. The target is an x86-64 Unraid NAS, so images are built for `linux/amd64` only.

## Settled choices

- **Controllers, not minimal APIs.** Clearer grouping across auth/coffees/reviews while
  learning. Minimal APIs are the modern alternative and would also have worked.
- **Flavor tags as a full many-to-many** (`FlavorTag` + join table) rather than a
  denormalized string column — the correct model, and better EF Core practice.
- **OCR behind `IOcrService`.** The engine is an implementation detail so it can be
  swapped (PaddleOCR, RapidOCR) without touching the application layer. The shipped
  adapter shells out to the `tesseract` CLI rather than binding a native library —
  see [CLAUDE.md](../CLAUDE.md) § OCR for why the P/Invoke route was abandoned.
- **First account becomes administrator**, as a deliberate bootstrap. A fresh instance
  accepts exactly one registration and then closes, so an internet-exposed instance is
  never open to signup by default and needs no environment variable to say so.

## Security

Instances may be internet-exposed and shared, so:

- **No default JWT signing key.** The app **fails to start** if the key is missing or
  weak — never a baked-in default. Key and connection string are injected at runtime
  only, never in the image or in git.
- **The app keeps its own login.** Every endpoint requires a token; a global fallback
  authorization policy means an endpoint cannot be left public by forgetting
  `[Authorize]`. The reverse proxy is not the authentication.
- **TLS terminates at the reverse proxy.** The container speaks HTTP and trusts
  `X-Forwarded-*` only from proxies named in `ForwardedHeaders:KnownProxies` — absent
  that, the headers are ignored, which is the secure default but also collapses
  rate-limiting onto the proxy's single IP.
- **Short-lived access tokens plus rotating, revocable refresh tokens.** Reuse of a
  rotated token revokes the whole session family.
- **Rate limits** on login/register, the anonymous config endpoint, and label scanning —
  the paths whose cost an anonymous or single caller could otherwise impose at will.
- **Uploads** are content-type allowlisted, magic-byte sniffed, size- and
  pixel-capped, and fully re-encoded (which strips any embedded payload or metadata).
  Filenames are server-generated. Photos are served only through short-lived signed
  URLs, never anonymously.
- **Response headers** carry a content security policy with no inline script, plus
  `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer` and `nosniff`.
- **Container** runs as a non-root user; only `/config` and `/photos` are writable.
- **Supply chain:** Dependabot across all seven ecosystems, CodeQL, and a Trivy image
  scan that hard-gates on fixable criticals.

## Conventions

- **DTOs at the boundary.** Entities are never serialized directly.
- **Config and secrets:** dev values in `appsettings`, production via environment
  variables. Never commit real keys — `.env` is gitignored for exactly this reason.
- **Comments explain why, not what.** See [CLAUDE.md](../CLAUDE.md) § Comment style;
  it is the rule this codebase is most deliberate about.
- **Feature branch → PR → CI green → squash-merge.** Linear history, no merge commits.

## Backups

`coffee.db` and `photos/` should be backed up **before pulling a new image**: a failed
startup auto-migration has no rollback, and the schema change survives a rollback of the
container even though the old code does not expect it.

In WAL mode the live database is **three files** (`coffee.db`, `-wal`, `-shm`). For a
consistent single-file snapshot use `sqlite3 coffee.db ".backup backup.db"` rather than
copying `coffee.db` alone.

## Known risks

- **OCR accuracy on real bags** is the biggest unknown. `IOcrService` is the designed
  escape hatch to another engine.
- **EF auto-migration on startup** has no rollback and assumes a single instance. WAL
  eases single-writer contention but does not change that assumption.
- **Public-instance abuse surface** — mitigated by closed-by-default registration,
  rate limiting, TLS at the proxy, and a non-root, least-writable container.
