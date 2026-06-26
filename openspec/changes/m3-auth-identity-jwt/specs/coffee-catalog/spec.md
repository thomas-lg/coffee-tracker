## ADDED Requirements

### Requirement: All catalog endpoints require authentication

An account is mandatory to use the app, so the system SHALL require a valid bearer token for every catalog endpoint — reads (`GET /api/coffees`, `GET /api/coffees/{id}`) as well as writes (`POST`/`PUT`/`DELETE /api/coffees`, `POST /api/coffees/{id}/photo`). Requests without a valid token SHALL be rejected with `401`. Only the authentication endpoints (`register`/`login`) are anonymous.

#### Scenario: Unauthenticated request is rejected

- **WHEN** a client without a valid token calls any `/api/coffees` endpoint (read or write)
- **THEN** the system SHALL respond with `401`
- **AND** SHALL NOT read or modify any data

#### Scenario: Authenticated request succeeds

- **WHEN** a client with a valid token sends a well-formed catalog request
- **THEN** the system SHALL process it as it would have before authentication was required

### Requirement: Created coffees record their owner

When a coffee is created by an authenticated user, the system SHALL record that user's id as the coffee's creator.

#### Scenario: Creator id is stamped on create

- **WHEN** an authenticated user creates a coffee
- **THEN** the stored coffee's `CreatedByUserId` SHALL be the authenticated user's id
