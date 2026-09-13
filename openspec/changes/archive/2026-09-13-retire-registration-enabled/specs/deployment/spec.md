## MODIFIED Requirements

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
