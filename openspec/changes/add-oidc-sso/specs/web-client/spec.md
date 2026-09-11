## ADDED Requirements

### Requirement: The sign-in screen offers the configured provider

The web client SHALL offer a provider sign-in action when `GET /api/config` reports a provider is available, and SHALL NOT render any provider affordance when none is. Choosing it SHALL run the Authorization Code flow with PKCE against the provider and exchange the result for an app session, after which the client SHALL behave exactly as it does after a local sign-in.

#### Scenario: Provider action is hidden when unconfigured

- **WHEN** the client loads and `GET /api/config` reports no provider available
- **THEN** the client SHALL NOT offer a provider sign-in action

#### Scenario: Signing in through the provider persists the session

- **WHEN** a user completes provider sign-in
- **THEN** the client SHALL store the returned token and attach it as a bearer token on subsequent API calls
- **AND** the session SHALL survive a page reload until the token expires

#### Scenario: A refused provider sign-in reports why

- **WHEN** the API refuses a provider sign-in
- **THEN** the client SHALL return to the sign-in screen with the reason shown
- **AND** SHALL NOT store a session

### Requirement: An administrator can control local accounts

The web client SHALL provide an administrator-only control for whether app-created accounts may register and sign in, reflecting the current setting and applying a change through the API. When the API refuses to disable local accounts, the client SHALL show the refusal and leave the control in its current state. The control SHALL NOT be reachable by non-administrators.

#### Scenario: Only admins can reach the control

- **WHEN** a non-administrator navigates to the admin settings route
- **THEN** the client SHALL redirect them away from it
- **AND** SHALL NOT show a navigation entry for it

#### Scenario: Toggling local accounts

- **WHEN** an administrator changes the local-accounts control
- **THEN** the client SHALL apply the change through the API
- **AND** SHALL reflect the stored value after the change

#### Scenario: A refused change is explained

- **WHEN** an administrator tries to disable local accounts and the API refuses because no administrator has signed in through the provider
- **THEN** the client SHALL show that explanation
- **AND** the control SHALL remain enabled

## MODIFIED Requirements

### Requirement: A visitor can authenticate through the web client

The web client SHALL let a visitor sign in, and register when local accounts are enabled, then keep them signed in across reloads. It SHALL read `GET /api/config` to decide whether to offer local sign-in and registration, persist the issued token, attach it to API requests, and return to the login screen when the API rejects the token.

#### Scenario: Register is hidden when disabled

- **WHEN** the client loads and `GET /api/config` reports local accounts are not enabled
- **THEN** the client SHALL NOT offer a registration option

#### Scenario: Local sign-in is hidden when disabled

- **WHEN** the client loads and `GET /api/config` reports local accounts are not enabled
- **THEN** the client SHALL NOT offer the email and password form

#### Scenario: Signing in persists the session

- **WHEN** a user logs in with valid credentials
- **THEN** the client SHALL store the returned token and attach it as a bearer token on subsequent API calls
- **AND** the session SHALL survive a page reload until the token expires

#### Scenario: An expired or rejected token returns to login

- **WHEN** an API call responds `401`
- **THEN** the client SHALL clear the stored session and route to the login screen
