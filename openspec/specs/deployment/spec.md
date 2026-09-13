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

The container SHALL allow the first account to be registered on a fresh instance without any environment variable being set, and SHALL close registration once an account exists. The container SHALL NOT read any environment variable governing registration. `REGISTRATION_ENABLED` SHALL have no effect and SHALL NOT appear in the image's default configuration, the reference compose file, or the documented environment variables.

#### Scenario: A fresh container needs no configuration to bootstrap

- **WHEN** a container starts against an empty database with no OIDC configuration
- **THEN** an operator SHALL be able to register the first account
- **AND** that account SHALL be an administrator

#### Scenario: A container upgraded from before the persisted policy starts closed

- **WHEN** a container starts for the first time against a database that already has users and no settings row
- **THEN** local sign-in SHALL be enabled, so existing users can still get in
- **AND** local registration SHALL be disabled until an administrator opens it from the admin view

#### Scenario: Setting the retired variable changes nothing

- **WHEN** an operator sets `REGISTRATION_ENABLED` and starts the container
- **THEN** the account policy SHALL be whatever the database holds
- **AND** startup SHALL be unaffected
