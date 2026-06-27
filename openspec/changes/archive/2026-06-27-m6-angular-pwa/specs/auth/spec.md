## ADDED Requirements

### Requirement: Registration availability is discoverable

The system SHALL expose, without authentication, whether open registration is currently enabled, via `GET /api/config`, so a client can show or hide the register option before any user signs in.

#### Scenario: Reading public client config

- **WHEN** any client requests `GET /api/config`
- **THEN** the system SHALL respond `200` with a body reporting whether registration is enabled
- **AND** SHALL NOT require authentication

#### Scenario: Config reflects the registration flag

- **WHEN** `REGISTRATION_ENABLED` is off
- **THEN** `GET /api/config` SHALL report registration as not enabled
- **AND** registration attempts SHALL still be refused by the register endpoint
