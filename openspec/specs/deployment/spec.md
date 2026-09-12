# deployment Specification

## Purpose
Package Coffee Tracker as a single self-hosted production container that serves the Angular PWA and the API same-origin on port `8080`, with OCR bundled in the image, the SQLite database and uploaded photos persisted to volumes, running as a non-root user behind a TLS-terminating reverse proxy.

## Requirements

### Requirement: A single production container serves the app same-origin

The system SHALL build to one production image that serves the Angular PWA and the
API from the same origin on port `8080`. Client-side routes SHALL resolve to the SPA
shell, while `/api`, `/photos`, and (in dev only) `/openapi` are handled first.

#### Scenario: Serving the app

- **WHEN** the container is run and a browser requests `/`
- **THEN** it SHALL return the Angular app, and API calls to `/api/...` on the same
  origin SHALL be served by the API without CORS

#### Scenario: Deep-linking a client route

- **WHEN** a user reloads a client route such as `/coffees/1`
- **THEN** the server SHALL return `index.html` so the SPA can render the route

### Requirement: The image bundles OCR and persists data to volumes

The production image SHALL include the Tesseract native libraries (OCR available by
default) and SHALL store the SQLite database under `/config` and uploaded photos
under `/photos`, so both survive container replacement.

#### Scenario: OCR available in the image

- **WHEN** an authenticated user posts a bag photo to `/api/coffees/scan` in the
  container
- **THEN** OCR SHALL run and return extracted text (not the host's 503-unavailable)

#### Scenario: Data persists across updates

- **WHEN** the container is recreated with the same `/config` and `/photos` volumes
- **THEN** previously catalogued coffees and their photos SHALL still be present

### Requirement: The container runs hardened behind a TLS proxy

The container SHALL run as a non-root user with only `/config` and `/photos`
writable, SHALL require a strong `Jwt__Key` at startup (failing fast otherwise), and
SHALL emit HSTS while leaving TLS termination to the reverse proxy.

#### Scenario: Missing signing key

- **WHEN** the container starts without a strong `Jwt__Key`
- **THEN** it SHALL refuse to start rather than run with an insecure default

#### Scenario: Forwarded scheme honored

- **WHEN** requests arrive via the reverse proxy with `X-Forwarded-Proto: https`
- **THEN** the app SHALL treat them as HTTPS for redirect/security decisions
### Requirement: OIDC is configured by environment and optional

The container SHALL accept an optional OIDC configuration — at minimum a provider authority and a client id, plus optional scopes and an administrator claim mapping — and SHALL run unchanged when it is absent. A partial configuration SHALL prevent startup with a clear message rather than presenting a sign-in option that cannot work, matching the stance already taken on a missing signing key. The configuration SHALL name no specific identity product.

#### Scenario: No OIDC configuration runs as before

- **WHEN** the container starts with no OIDC variables set
- **THEN** it SHALL start normally
- **AND** SHALL report no provider available to clients

#### Scenario: Partial OIDC configuration fails fast

- **WHEN** the container starts with an authority but no client id, or the reverse
- **THEN** it SHALL refuse to start with a message naming the missing variable

#### Scenario: A slow or unreachable provider does not block startup

- **WHEN** the configured provider's discovery document cannot be fetched at startup
- **THEN** the container SHALL still start and serve requests
- **AND** SHALL report the provider as unavailable until discovery succeeds

### Requirement: No environment variable is required to create the first account

The container SHALL allow the first account to be registered on a fresh instance without any environment variable being set, and SHALL close registration once an account exists. `REGISTRATION_ENABLED` SHALL be read at most once, on an instance that already has users and no settings row, purely so an upgrade preserves that deployment's registration posture; it SHALL be ignored thereafter and SHALL be documented as legacy.

#### Scenario: A fresh container needs no configuration to bootstrap

- **WHEN** a container starts against an empty database with no OIDC and no `REGISTRATION_ENABLED` set
- **THEN** an operator SHALL be able to register the first account
- **AND** that account SHALL be an administrator

#### Scenario: An upgraded container keeps its registration posture

- **WHEN** a container that had `REGISTRATION_ENABLED` set starts for the first time after the migration, against a database that already has users
- **THEN** local registration SHALL take that value
- **AND** local sign-in SHALL be enabled

#### Scenario: The variable no longer takes effect

- **WHEN** an operator changes `REGISTRATION_ENABLED` and restarts a container whose settings row already exists
- **THEN** both settings SHALL be unchanged
