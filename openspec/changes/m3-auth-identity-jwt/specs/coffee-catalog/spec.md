## ADDED Requirements

### Requirement: Catalog write operations require authentication

The system SHALL require a valid bearer token for all catalog write operations — creating, updating, and deleting a coffee, and attaching a photo. Unauthenticated write requests SHALL be rejected with `401`. Read operations (`GET /api/coffees`, `GET /api/coffees/{id}`) SHALL remain available without authentication.

#### Scenario: Unauthenticated write is rejected

- **WHEN** a client without a valid token sends `POST`, `PUT`, or `DELETE /api/coffees` (or `POST /api/coffees/{id}/photo`)
- **THEN** the system SHALL respond with `401`
- **AND** SHALL NOT modify any data

#### Scenario: Authenticated write succeeds

- **WHEN** a client with a valid token sends a well-formed write request
- **THEN** the system SHALL process it as it would have before authentication was required

#### Scenario: Reads remain public

- **WHEN** a client without a token sends `GET /api/coffees` or `GET /api/coffees/{id}`
- **THEN** the system SHALL respond normally without requiring authentication

### Requirement: Created coffees record their owner

When a coffee is created by an authenticated user, the system SHALL record that user's id as the coffee's creator.

#### Scenario: Creator id is stamped on create

- **WHEN** an authenticated user creates a coffee
- **THEN** the stored coffee's `CreatedByUserId` SHALL be the authenticated user's id
