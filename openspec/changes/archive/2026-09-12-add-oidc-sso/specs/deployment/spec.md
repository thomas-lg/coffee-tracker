## ADDED Requirements

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
