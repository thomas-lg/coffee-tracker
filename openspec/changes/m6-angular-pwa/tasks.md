## 1. Workspace scaffold

- [x] 1.1 npm-workspace monorepo in `frontend/`: `app` + `@coffee-tracker/{ui,util,auth,data,coffees}` (flat, scoped, source path aliases)
- [x] 1.2 Zoneless bootstrap; router (view transitions + input binding); functional HttpClient
- [x] 1.3 Tailwind v4 + coffee `@theme` tokens; light/dark via `[data-theme]`; self-hosted Fraunces + Inter; reduced-motion base
- [x] 1.4 `@angular/pwa` (manifest, service worker, icons, offline shell); `proxy.conf.json` → API
- [x] 1.5 Install gsap + three (lazy later) + lucide core; validate prod build + `ng serve`

## 2. Core (`data`, `auth`, `util`, `ui`)

- [ ] 2.1 `data`: TS DTO models mirroring the API; HTTP data-access services (coffees, reviews, flavor tags, scan); `config` service (`GET /api/config`)
- [ ] 2.2 `auth`: signal-based auth service (token in localStorage), functional interceptor (attach JWT; 401 → logout), auth guard, login/register data calls
- [ ] 2.3 `util`: motion helpers (GSAP wrappers + reduced-motion guard), formatting, theme helpers
- [ ] 2.4 `ui` primitives: button (done), card, animated rating, flavor chips, skeleton, toast, `ct-icon` (lucide)

## 3. Features (`coffees`, auth screens)

- [ ] 3.1 Auth screens: login + register via Signal Forms; hide register when `registrationEnabled` is false
- [ ] 3.2 Coffee **grid**: search/filter, average rating + count, stagger-in, hover
- [ ] 3.3 Coffee **detail**: photo + info, ratings-over-time timeline + "rate today", everyone's reviews + average, lazy 3D hero
- [ ] 3.4 **Add/edit** (Signal Forms → coffee DTOs) + photo upload
- [ ] 3.5 **Snap-to-fill**: capture → `POST /api/coffees/scan` → prefill Add form, reuse photo; handle `503`

## 4. Shell, PWA & theme

- [ ] 4.1 App shell: header (brand, nav, theme toggle, account), layout, route transitions
- [ ] 4.2 Theme toggle (OS default + manual, persisted); confirm offline shell + installability

## 5. Backend config endpoint (shipped — captured here)

- [x] 5.1 Anonymous `GET /api/config` → `{ registrationEnabled }` via `IAuthService.RegistrationEnabled`
- [x] 5.2 Development-only CORS for `http://localhost:4200`

## 6. CI & verify

- [ ] 6.1 Drop the `frontend/package.json` existence guard in `ci.yml` (frontend job runs for real)
- [ ] 6.2 `ng build` (prod) + `ng test` (Vitest) green
- [ ] 6.3 E2E against the API (register/login, browse/search, detail + rate over time, add/edit + photo, snap-to-fill)
- [ ] 6.4 Phone-first: mobile viewport, Lighthouse PWA installable + perf budget (hero lazy), `prefers-reduced-motion`
