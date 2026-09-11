# Test plan — optional OIDC sign-in and the local-account policy

What the automated suites already prove, what only a running instance can show, and
what needs a real identity provider. Each phase says how to run it and what result
counts as a pass, so a failure is a fact rather than an impression.

The risk this plan is built around: **this change can lock an operator out of their own
instance.** Every phase that touches the account policy checks the door is still open.

## Phase 1 — Automated suites (repeatable, no setup)

| # | What | How | Pass |
|---|---|---|---|
| 1.1 | Backend unit + integration | `dotnet test CoffeeTracker.sln` | 0 failed |
| 1.2 | Frontend unit | `cd frontend && npx ng test --watch=false` | 0 failed |
| 1.3 | Frontend production build | `cd frontend && npx ng build app` | Completes, no budget warning |
| 1.4 | Lint | `cd frontend && npx eslint packages` | No errors |
| 1.5 | Generated API types match the API | Regenerate `openapi.json`, `npm run gen:api`, then 1.3 | No diff, build still passes |

## Phase 2 — A fresh instance, end to end (local, no provider)

The self-hoster's first five minutes. Run against an empty database with **no** `Oidc`
configuration and **no** `REGISTRATION_ENABLED`.

| # | Step | Expected |
|---|---|---|
| 2.1 | Start the API against an empty DB | Starts; no OIDC configuration needed |
| 2.2 | `GET /api/config` | `localLoginEnabled: true`, `registrationEnabled: true`, `oidcAvailable: false`, `oidc: null` |
| 2.3 | Register the first account | 200, `isAdmin: true` |
| 2.4 | `GET /api/config` again | `registrationEnabled` is now **false** — the door closed itself |
| 2.5 | Register a second account | 403 |
| 2.6 | Sign in with the first account | 200 |
| 2.7 | Load the login screen in a browser | Email/password form shown, no provider button, no "create one" link |

## Phase 3 — The admin policy and its guard (local, no provider)

| # | Step | Expected |
|---|---|---|
| 3.1 | `GET /api/admin/settings` anonymously | 401 |
| 3.2 | …as a non-admin | 403 |
| 3.3 | …as the admin | 200, both flags |
| 3.4 | Admin reopens registration | 200; a second account can now register |
| 3.5 | Register a third account after 3.4 | 200 — registration an admin opened does **not** close itself |
| 3.6 | Admin tries to disable local sign-in | **409**, with an explanation naming the identity provider |
| 3.7 | `GET /api/admin/settings` after 3.6 | `localLoginEnabled` still true — the refusal changed nothing |
| 3.8 | Admin closes registration | 200 |
| 3.9 | Open `/admin/settings` as a non-admin | Redirected away, no nav entry |
| 3.10 | Open `/admin/settings` as the admin, toggle each switch | Values persist across a reload |
| 3.11 | Trigger 3.6 from the UI | The refusal is shown inline and the switch stays on |

## Phase 4 — The upgrade path (the part that can lock people out)

Run against a **copy** of a database that predates this change, never the live one.

| # | Step | Expected |
|---|---|---|
| 4.1 | Copy the production DB; start with `REGISTRATION_ENABLED=false` | Migration applies; **sign-in still works** for an existing account |
| 4.2 | `GET /api/config` | `localLoginEnabled: true`, `registrationEnabled: false` — the posture is preserved |
| 4.3 | Restart with `REGISTRATION_ENABLED=true` | Both settings unchanged — the variable is spent |
| 4.4 | Same from a copy that had `REGISTRATION_ENABLED=true` | `registrationEnabled: true` |

## Phase 5 — Provider configuration (local, no real provider needed)

| # | Step | Expected |
|---|---|---|
| 5.1 | Start with `Oidc__Authority` but no `Oidc__ClientId` | Refuses to start, message names `ClientId` |
| 5.2 | Start with `Oidc__ClientId` but no `Oidc__Authority` | Refuses to start, message names `Authority` |
| 5.3 | Start with `Oidc__AdminClaim` but no `Oidc__AdminClaimValue` | Refuses to start, message names `AdminClaimValue` |
| 5.4 | Start with an authority that does not resolve | **Starts anyway**; `oidcAvailable: false`; local sign-in unaffected |
| 5.5 | `POST /api/auth/oidc` with any token, no provider configured | 404 |

## Phase 6 — A real provider, end to end

Needs an OIDC client registered at a real provider. Authelia on the NAS is the obvious
candidate, and registering a client there is a change to live infrastructure — **to be
agreed before touching it.**

| # | Step | Expected |
|---|---|---|
| 6.1 | Register a public client (code + PKCE, redirect to the app root) | Discovery document reachable from the app |
| 6.2 | Start the app with `Oidc__Authority` and `Oidc__ClientId` | `oidcAvailable: true`, `oidc` carries authority and client id |
| 6.3 | Login screen | Provider button shown, above the local form, separated by "or" |
| 6.4 | Sign in through the provider, first time, on a fresh account | Account created, session established, admin by bootstrap |
| 6.5 | Sign out and sign in again | **Same** account (matched on subject, not email) |
| 6.6 | Change the account's email at the provider, sign in again | Still the same account |
| 6.7 | Replay a captured ID token against `POST /api/auth/oidc` | 401 — a token buys one session |
| 6.8 | Create a local account with an email the provider will assert, then sign in through the provider | 409 if the provider does not assert `email_verified`; linked to that account if it does |
| 6.9 | Configure `Oidc__AdminClaim`/`Value` matching a group the user holds | Signing in grants admin |
| 6.10 | Remove the user from that group, sign in again | Admin is **revoked** |
| 6.11 | As an admin who signed in through the provider, disable local sign-in | 200 — the guard is satisfied |
| 6.12 | Sign out, reload the login screen | Only the provider button; no email/password form |
| 6.13 | `POST /api/auth/login` with valid credentials | 403 |
| 6.14 | Sign back in through the provider, re-enable local sign-in | 200, and the form returns |

## Phase 7 — Regression surface

| # | What | Why it is here |
|---|---|---|
| 7.1 | Register, log in, log out, refresh a session | The change touched `AuthService` and `ConfigDto` |
| 7.2 | Create, edit and delete a coffee as its owner | `IsAdmin` is now settable from a claim |
| 7.3 | A non-admin cannot edit someone else's coffee | Same |
| 7.4 | `/admin/photos` still admin-only | The admin policy gained a second consumer |
| 7.5 | PWA still installs and serves offline | The initial bundle grew |

## What this plan does not cover

- **A token stolen before the legitimate client exchanges it.** Single use closes replay,
  not a race. Only the backend-for-frontend flow would, and it is out of scope by design.
- **Providers other than the one used in phase 6.** The implementation depends only on
  the discovery document, but "standards-compliant" is a claim this plan tests once.
- **Concurrency on the settings row.** Single-instance SQLite deployment; two admins
  racing on the same toggle is not a scenario worth building for here.
