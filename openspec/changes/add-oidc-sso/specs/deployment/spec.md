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

### Requirement: The local-account setting is seeded from the previous flag

On the first startup after the migration that introduces it, the container SHALL seed the persisted local-account setting from the current `REGISTRATION_ENABLED` value, so an existing deployment keeps its behaviour without operator action. Thereafter the variable SHALL be ignored and the setting SHALL be changed from the admin view.

#### Scenario: Existing deployment keeps its behaviour

- **WHEN** a container that had `REGISTRATION_ENABLED` set starts for the first time after the migration
- **THEN** the persisted setting SHALL take that value
- **AND** registration SHALL behave as it did before the upgrade

#### Scenario: The variable no longer takes effect

- **WHEN** an operator changes `REGISTRATION_ENABLED` and restarts a container whose setting is already seeded
- **THEN** the persisted setting SHALL be unchanged
