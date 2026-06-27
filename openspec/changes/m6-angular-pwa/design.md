## 1. npm-workspace monorepo, split into libraries

The frontend is a **true npm workspace** (`packages/*`, each lib a real package),
not the Angular-CLI single-workspace. One `app` (shell/routing/PWA) plus
buildable libraries `@coffee-tracker/{ui,util,auth,data,coffees}`. tsconfig path
aliases point at each lib's `src/public-api.ts` (source, not `dist`) so dev HMR is
instant; ng-packagr still builds publishable libs for prod. `coffees` may split by
feature later. Rejected Nx (heavier, wraps workspaces) and the CLI single-workspace
(no per-package boundaries).

## 2. State-of-the-art Angular 22

Zoneless change detection (`provideZonelessChangeDetection`, no Zone.js), signals
everywhere (incl. signal inputs), standalone components, native control flow
(`@if`/`@for`), `@defer` for the lazy 3D hero, the `@angular/build` (esbuild/vite)
builder, Vitest, router with `withViewTransitions()` + `withComponentInputBinding()`,
functional `HttpClient`/guards/interceptors, `inject()` over constructor DI.

## 3. Tailwind v4 + token theme, light/dark

Tailwind v4 via `@tailwindcss/postcss`. The coffee palette is defined as CSS
variables and mapped through `@theme inline` so utilities (`bg-foam`, `text-ink`)
resolve the live variable — which means the theme flips by toggling
`[data-theme="dark"]` on `<html>` (OS preference is the default; a manual toggle
overrides). Fonts (Fraunces display, Inter body) are self-hosted via fontsource —
no CDN, so the PWA works offline. The global Tailwind entry is plain `.css`
(Tailwind v4 is CSS-first; `@import "tailwindcss"` in `.scss` is a deprecated Sass
import); component styles remain `.scss`.

## 4. Relative API URLs + dev proxy

The client always calls **relative** paths (`/api/...`, `/photos/...`). In dev,
`proxy.conf.json` routes those to `http://localhost:5000` (same-origin to the
browser → no CORS needed; the dev CORS policy is a belt-and-suspenders fallback).
In prod (M7) the API serves the SPA same-origin, so the same relative URLs work
with no configuration.

## 5. Auth: client-stored JWT + functional interceptor

The token is kept in `localStorage` and exposed via signals in `@coffee-tracker/auth`;
a functional interceptor attaches `Authorization: Bearer …`; a `401` clears the
session and routes to login; an auth guard protects routes. Register is gated by
`GET /api/config`. Trade-off: `localStorage` is XSS-readable (vs an HttpOnly
cookie). Accepted for a self-hosted, single-origin app behind a reverse proxy; the
token is short-lived and revocation is a restart concern, consistent with the
backend's stateless-JWT design.

## 6. Motion, phone-first

One signature **lazy-loaded** 3D moment (three, via `@defer`/dynamic import) so it
never bloats first paint; GSAP + Angular animations for everyday motion (card
stagger, hover, animated ratings, route/view transitions). All motion is gated on
`prefers-reduced-motion`, and a perf budget keeps it smooth on mid-range phones.

## 7. Ratings over time

The coffee detail renders the user's **timeline** of dated ratings (from the
`reviews-over-time` backend change) plus everyone's reviews and the average, with a
simple "rate it today" control. No one-per-user constraint anymore.

## 8. Models: hand-written now, OpenAPI-generatable later

TS DTO interfaces live in `@coffee-tracker/data`, hand-written for now. The API
emits an OpenAPI document, so these could later be **auto-generated** (e.g.
`openapi-typescript`, types-only) and regenerated in CI to eliminate drift from the
C# DTOs — a deliberate future option, not adopted in this change.
