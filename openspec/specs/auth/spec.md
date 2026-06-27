# auth Specification

## Purpose
TBD - created by archiving change m3-auth-identity-jwt. Update Purpose after archive.
## Requirements
### Requirement: Users can register when registration is enabled

The system SHALL expose `POST /api/auth/register` accepting a display name, email/username, and password. Registration SHALL be permitted only when the `REGISTRATION_ENABLED` flag is true; when it is false the system SHALL refuse all registration attempts. Passwords SHALL be subject to a configured password policy. The first user successfully registered on an instance SHALL be made an administrator; subsequent users SHALL NOT.

#### Scenario: Registration is refused when disabled

- **WHEN** `REGISTRATION_ENABLED` is false and a client sends `POST /api/auth/register`
- **THEN** the system SHALL refuse the request
- **AND** SHALL NOT create a user

#### Scenario: First registered user becomes admin

- **WHEN** registration is enabled and the first-ever user registers with a valid payload
- **THEN** the system SHALL create the user
- **AND** SHALL mark that user as an administrator

#### Scenario: Later users are not admins

- **WHEN** registration is enabled and at least one user already exists
- **THEN** a newly registered user SHALL NOT be an administrator

#### Scenario: Weak passwords are rejected

- **WHEN** a registration password does not meet the password policy
- **THEN** the system SHALL reject the registration
- **AND** SHALL NOT create a user

### Requirement: Users can authenticate and receive a token

The system SHALL expose `POST /api/auth/login` that verifies credentials and, on success, returns a signed JWT carrying the user's id and administrator status. Invalid credentials SHALL be rejected without revealing whether the username or the password was wrong. Repeated failed attempts SHALL trip account lockout.

#### Scenario: Successful login returns a token

- **WHEN** a client logs in with valid credentials
- **THEN** the system SHALL respond with a signed JWT
- **AND** the token SHALL contain the user's id and administrator claim

#### Scenario: Invalid credentials are rejected

- **WHEN** a client logs in with an unknown user or wrong password
- **THEN** the system SHALL reject the login with an unauthorized response
- **AND** SHALL NOT issue a token

#### Scenario: Repeated failures lock the account

- **WHEN** a client exceeds the configured number of failed login attempts
- **THEN** the system SHALL lock the account for the configured duration

### Requirement: The JWT signing key is required and never defaulted

The system SHALL read the JWT signing key only from configuration/environment and SHALL fail to start if the key is missing or weaker than the minimum safe length. The system SHALL NOT ship or fall back to a built-in default signing key.

#### Scenario: Missing or weak key prevents startup

- **WHEN** the application starts without a configured signing key, or with one below the minimum length
- **THEN** the application SHALL fail to start with a clear error
- **AND** SHALL NOT serve requests

### Requirement: Authentication endpoints are rate limited

The system SHALL rate-limit the registration and login endpoints to throttle brute-force and credential-stuffing attempts, responding with a too-many-requests status when the limit is exceeded.

#### Scenario: Excessive auth requests are throttled

- **WHEN** a client exceeds the configured request rate on `register` or `login`
- **THEN** the system SHALL respond with a too-many-requests status
- **AND** SHALL NOT process the throttled request

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

