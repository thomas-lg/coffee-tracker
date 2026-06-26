## ADDED Requirements

### Requirement: A visitor can authenticate through the web client

The web client SHALL let a visitor sign in, and register when registration is enabled, then keep them signed in across reloads. It SHALL read `GET /api/config` to decide whether to offer registration, persist the issued token, attach it to API requests, and return to the login screen when the API rejects the token.

#### Scenario: Register is hidden when disabled

- **WHEN** the client loads and `GET /api/config` reports registration is not enabled
- **THEN** the client SHALL NOT offer a registration option

#### Scenario: Signing in persists the session

- **WHEN** a user logs in with valid credentials
- **THEN** the client SHALL store the returned token and attach it as a bearer token on subsequent API calls
- **AND** the session SHALL survive a page reload until the token expires

#### Scenario: An expired or rejected token returns to login

- **WHEN** an API call responds `401`
- **THEN** the client SHALL clear the stored session and route to the login screen

### Requirement: A user can browse and search the coffee catalog

The web client SHALL present coffees as a responsive card grid showing each coffee's photo, name, roaster, and average rating with review count, and SHALL let the user search and filter the list.

#### Scenario: Viewing the catalog

- **WHEN** an authenticated user opens the catalog
- **THEN** the client SHALL show the coffees as cards with their average rating and review count

#### Scenario: Filtering the catalog

- **WHEN** the user enters a search term or selects a filter
- **THEN** the client SHALL show only the matching coffees

### Requirement: A user can view a coffee and its ratings over time

The web client SHALL show a coffee's details with everyone's reviews and the average rating, plus the current user's own ratings as a dated timeline, and SHALL let the user add a new dated rating at any time.

#### Scenario: Viewing ratings over time

- **WHEN** a user opens a coffee they have rated more than once
- **THEN** the client SHALL show their ratings as a timeline ordered newest-first alongside the average

#### Scenario: Adding a rating today

- **WHEN** the user submits a new rating for the coffee
- **THEN** the client SHALL create it as a new dated entry and reflect it in the timeline

### Requirement: A user can add or edit a coffee, with snap-to-fill

The web client SHALL let a user create and edit coffees through validated forms, upload a photo, and pre-fill the Add form by photographing a bag.

#### Scenario: Snap-to-fill pre-fills the form

- **WHEN** the user captures or selects a bag photo on the Add screen
- **THEN** the client SHALL send it to the scan endpoint and pre-fill the form from the parsed fields, reusing the returned photo
- **AND** the user SHALL be able to correct any field before saving

#### Scenario: OCR unavailable is handled gracefully

- **WHEN** the scan endpoint responds that OCR is unavailable
- **THEN** the client SHALL let the user fill the form manually without error

### Requirement: The client is an installable, offline-capable PWA

The web client SHALL be installable to the home screen and SHALL load an app shell when offline.

#### Scenario: Installing the app

- **WHEN** the app is served over a secure context
- **THEN** it SHALL be installable and SHALL register a service worker that serves the shell offline

### Requirement: The client supports light/dark theming and reduced motion

The web client SHALL provide a warm light and dark theme, defaulting to the operating-system preference with a manual override, and SHALL respect `prefers-reduced-motion` by disabling non-essential animation.

#### Scenario: Following the OS theme

- **WHEN** the app first loads
- **THEN** it SHALL apply the light or dark theme matching the OS preference

#### Scenario: Reduced motion is honored

- **WHEN** the user's OS requests reduced motion
- **THEN** the client SHALL disable non-essential animation
