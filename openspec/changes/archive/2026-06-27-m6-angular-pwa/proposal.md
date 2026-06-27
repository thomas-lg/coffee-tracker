## Why

M0–M5 built and hardened the API. M6 (in `PLAN.md`) delivers the **first
user-facing surface**: an installable **Angular 22 PWA** — warm, card-based,
animated, phone-first — so the app is actually usable, not just a Swagger page.
The visual + motion direction was locked via an interactive prototype; the backend
prerequisites (anonymous `GET /api/config`, dev-only CORS) already shipped, and the
`reviews-over-time` change reshaped ratings into a per-coffee timeline.

## What Changes

- New **`frontend/`** npm-workspace monorepo: `app` + libraries
  `@coffee-tracker/{ui,util,auth,data,coffees}` (scoped names, source path aliases).
  State-of-the-art Angular 22: zoneless, signals, standalone, native control flow,
  the esbuild/vite builder, Signal Forms, functional guards/interceptors.
- **Auth UI:** login + register (register shown only when `GET /api/config`
  reports `registrationEnabled`); JWT persisted client-side and attached via a
  functional HTTP interceptor; a `401` clears the session and routes to login; an
  auth guard protects the app.
- **Catalog:** responsive **coffee grid** (search/filter, average rating + count),
  **detail** (everyone's reviews + the user's **ratings-over-time** timeline, add a
  dated rating), **add/edit** (Signal Forms → coffee DTOs, photo upload).
- **Snap-to-fill:** camera/file capture → `POST /api/coffees/scan` → pre-fill the
  Add form from the parsed fields, reusing the returned photo; handle OCR `503`.
- **PWA & design:** manifest, service worker, offline shell, installable; warm
  light/dark theme (OS default + toggle); reduced-motion; a lazy-loaded 3D hero
  (three) + GSAP micro-interactions.
- **Backend (already shipped — documented here):** anonymous `GET /api/config`
  exposing `registrationEnabled`; Development-only CORS for the `ng serve` origin.
- **CI:** drop the `frontend/package.json` existence guard now that `frontend/`
  exists, so the frontend job runs its real steps.

Out of scope (deferred): admin photo-cleanup UI; M7 production image/compose;
SSO/OIDC; non-English OCR; auto-generating TS models from OpenAPI (possible, noted
in design as a future option).

## Capabilities

### New Capabilities
- `web-client`: the installable Angular PWA — authenticate, browse/search the
  catalog, view a coffee with its ratings over time, add/edit coffees, snap-to-fill
  from a bag photo, with light/dark theming, offline shell, and reduced-motion.

### Modified Capabilities
- `auth`: whether open registration is available is discoverable anonymously via
  `GET /api/config`.

## Impact

- **New `frontend/` workspace** — Angular 22, Tailwind v4, PWA; no impact on
  existing backend projects beyond the already-shipped config endpoint + dev CORS.
- **Backend (shipped):** `ConfigController`, `IAuthService.RegistrationEnabled`,
  dev CORS in `Program.cs`.
- **CI:** `ci.yml` frontend job stops being a no-op.
- **No database changes.**
