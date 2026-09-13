# auth Specification

## Purpose
TBD - created by archiving change m3-auth-identity-jwt. Update Purpose after archive.
## Requirements
### Requirement: Users can register when registration is enabled

The system SHALL expose `POST /api/auth/register` accepting a display name, email/username, and password. Registration SHALL be permitted only when the persisted local-registration setting is enabled; when it is disabled the system SHALL refuse all registration attempts. Passwords SHALL be subject to a configured password policy. The first user successfully registered on an instance SHALL be made an administrator; subsequent users SHALL NOT.

#### Scenario: Registration is refused when disabled

- **WHEN** local registration is disabled and a client sends `POST /api/auth/register`
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

The system SHALL expose `POST /api/auth/login` that verifies credentials and, on success, returns a signed JWT carrying the user's id and administrator status. Login SHALL be permitted only when the persisted local-sign-in setting is enabled. Invalid credentials SHALL be rejected without revealing whether the username or the password was wrong. Repeated failed attempts SHALL trip account lockout.

#### Scenario: Successful login returns a token

- **WHEN** a client logs in with valid credentials and local sign-in is enabled
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

The system SHALL expose, without authentication, via `GET /api/config`, whether local sign-in is currently accepted, whether local registration is open, and whether an external provider is available for sign-in, so a client can show the right options before any user signs in.

#### Scenario: Reading public client config

- **WHEN** any client requests `GET /api/config`
- **THEN** the system SHALL respond `200` with a body reporting whether local sign-in is enabled, whether local registration is open, and whether a provider is available
- **AND** SHALL NOT require authentication

#### Scenario: Config reflects the local-account settings

- **WHEN** local sign-in and local registration are disabled
- **THEN** `GET /api/config` SHALL report both as not enabled
- **AND** registration and login attempts SHALL still be refused by their endpoints

#### Scenario: Config reflects provider availability

- **WHEN** no OIDC provider is configured
- **THEN** `GET /api/config` SHALL report no provider available
### Requirement: Users can sign in through an external OIDC provider

When an OpenID Connect provider is configured, the system SHALL accept an ID token issued by that provider at `POST /api/auth/oidc` and, on success, return the same session payload as a local login: a signed JWT carrying the user's id and administrator status, plus a refresh token. The system SHALL validate the token's signature against the provider's published keys and SHALL reject any token whose issuer, audience or expiry does not match the configured provider. Each token SHALL buy at most one session. The system SHALL NOT depend on any provider-specific behaviour beyond the OpenID Connect discovery document.

#### Scenario: Valid provider token yields a session

- **WHEN** a client posts an ID token that validates against the configured provider
- **THEN** the system SHALL respond with a signed JWT and a refresh token
- **AND** the token SHALL carry the user's id and administrator claim

#### Scenario: Token from an unexpected issuer or audience is rejected

- **WHEN** a client posts an ID token whose issuer or audience does not match the configured provider
- **THEN** the system SHALL reject the request with an unauthorized response
- **AND** SHALL NOT create a user or issue a token

#### Scenario: Expired token is rejected

- **WHEN** a client posts an ID token that has expired
- **THEN** the system SHALL reject the request with an unauthorized response
- **AND** SHALL NOT issue a token

#### Scenario: A token cannot be exchanged twice

- **WHEN** a client posts an ID token that has already been exchanged for a session
- **THEN** the system SHALL reject the request with an unauthorized response
- **AND** SHALL NOT issue a token

#### Scenario: Provider sign-in is unavailable when unconfigured

- **WHEN** no OIDC provider is configured and a client posts to `POST /api/auth/oidc`
- **THEN** the system SHALL refuse the request
- **AND** SHALL NOT issue a token

### Requirement: A provider identity resolves to exactly one account

The system SHALL record each external identity as the pair of the provider's issuer and the token's subject claim, and SHALL resolve subsequent sign-ins by that pair rather than by email. On a first sign-in, the system SHALL attach the identity to an existing account when the token carries an email matching that account **and** asserts that the email is verified; when the email matches an existing account but is not asserted as verified, the system SHALL refuse the sign-in rather than linking or creating a second account. Otherwise the system SHALL create a new account.

#### Scenario: Returning user matches on issuer and subject

- **WHEN** a user who has signed in through the provider before signs in again
- **THEN** the system SHALL resolve the same account
- **AND** SHALL do so even if the email carried by the token has changed

#### Scenario: First sign-in links to an existing account on a verified email

- **WHEN** a user signs in for the first time through the provider with a verified email matching an existing account
- **THEN** the system SHALL attach the external identity to that account
- **AND** SHALL NOT create a second account

#### Scenario: Unverified matching email is refused

- **WHEN** a user signs in for the first time with an email matching an existing account, and the token does not assert that the email is verified
- **THEN** the system SHALL refuse the sign-in
- **AND** SHALL NOT link the identity or create an account

#### Scenario: Unknown identity creates an account

- **WHEN** a user signs in for the first time and no existing account matches
- **THEN** the system SHALL create an account and attach the external identity to it

### Requirement: Administrator status can be driven by a provider claim

When an administrator claim and value are configured, the system SHALL set the user's administrator status from that claim on every provider sign-in, granting it when the claim carries the configured value and revoking it when it does not. When no such mapping is configured, the first user to sign in through the provider on an instance with no administrator SHALL become one, and subsequent users SHALL NOT.

#### Scenario: Claim grants administrator status

- **WHEN** an administrator claim is configured and a user signs in with a token carrying the configured value
- **THEN** the system SHALL mark that user as an administrator

#### Scenario: Losing the claim revokes administrator status

- **WHEN** an administrator claim is configured and a user who is currently an administrator signs in with a token no longer carrying the configured value
- **THEN** the system SHALL remove that user's administrator status

#### Scenario: Bootstrap applies when no mapping is configured

- **WHEN** no administrator claim is configured and the first-ever user signs in through the provider
- **THEN** the system SHALL mark that user as an administrator
- **AND** subsequent provider sign-ins SHALL NOT produce administrators

### Requirement: An administrator controls local sign-in and local registration

The system SHALL persist two settings — whether accounts created in the app may sign in, and whether new ones may be registered — SHALL expose both to administrators for reading and updating, and SHALL enforce each on the corresponding endpoint. The settings SHALL survive restarts and SHALL be changeable without redeploying.

#### Scenario: Disabling local sign-in refuses local login

- **WHEN** local sign-in is disabled and a client posts valid credentials to `POST /api/auth/login`
- **THEN** the system SHALL refuse the request with a reason distinct from invalid credentials
- **AND** SHALL NOT issue a token

#### Scenario: Disabling local registration refuses registration

- **WHEN** local registration is disabled and a client posts to `POST /api/auth/register`
- **THEN** the system SHALL refuse the request
- **AND** SHALL NOT create a user

#### Scenario: The two settings are independent

- **WHEN** local registration is enabled and local sign-in is disabled
- **THEN** registration SHALL succeed
- **AND** the resulting account SHALL NOT be able to sign in until local sign-in is re-enabled

#### Scenario: Provider sign-in is unaffected

- **WHEN** local sign-in is disabled and a user signs in through the configured provider
- **THEN** the system SHALL issue a token as usual

#### Scenario: Only administrators may change the settings

- **WHEN** a non-administrator attempts to update either setting
- **THEN** the system SHALL refuse the request
- **AND** SHALL leave both settings unchanged

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

### Requirement: Local sign-in cannot be disabled while it is the only way in

The system SHALL refuse to disable local sign-in unless at least one administrator has already completed a sign-in through the configured provider, and SHALL explain the refusal so an administrator can act on it. Re-enabling local sign-in SHALL never be refused. Local registration carries no such condition, since disabling it cannot lock anyone out.

#### Scenario: Disabling is refused with no proven provider administrator

- **WHEN** an administrator attempts to disable local sign-in and no administrator has ever signed in through the provider
- **THEN** the system SHALL refuse the request with an explanation
- **AND** the setting SHALL remain enabled

#### Scenario: Disabling is allowed once an administrator has signed in through the provider

- **WHEN** an administrator who has signed in through the provider attempts to disable local sign-in
- **THEN** the system SHALL apply the change

#### Scenario: Re-enabling is always allowed

- **WHEN** an administrator re-enables local sign-in
- **THEN** the system SHALL apply the change without further condition
