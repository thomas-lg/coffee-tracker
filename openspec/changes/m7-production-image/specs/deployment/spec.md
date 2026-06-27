## ADDED Requirements

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
