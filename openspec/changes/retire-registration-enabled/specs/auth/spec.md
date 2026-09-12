## MODIFIED Requirements

### Requirement: A fresh instance accepts its first account without configuration

The system SHALL create the settings row once, when it is absent. Sign-in SHALL always be seeded enabled, so no instance can be shut out by a setting that was merely never written. Registration SHALL be seeded enabled only on an instance with no users, so the first account can be created with nothing configured, and SHALL be disabled as soon as an account exists. On an instance that already has users, registration SHALL be seeded disabled, leaving an administrator to open it from the admin view. No environment variable SHALL be required to create the first account, and no environment variable SHALL affect either setting.

#### Scenario: A fresh instance allows the first registration

- **WHEN** an instance with no users and no settings row starts, and a client registers
- **THEN** the system SHALL create the user
- **AND** SHALL mark that user as an administrator

#### Scenario: Registration closes itself after the first account

- **WHEN** the first account has been created on a fresh instance
- **THEN** local registration SHALL be disabled
- **AND** a further registration attempt SHALL be refused until an administrator re-enables it

#### Scenario: An instance that already has users is seeded closed

- **WHEN** an instance that already has users and no settings row starts
- **THEN** local sign-in SHALL be enabled
- **AND** local registration SHALL be disabled
- **AND** the settings row SHALL NOT be marked as bootstrap-opened, so registration an administrator opens stays open

#### Scenario: Seeding happens at most once

- **WHEN** the settings row already exists and the instance restarts
- **THEN** both settings SHALL be unchanged
