## ADDED Requirements

### Requirement: Coffee records are persisted in a database

The system SHALL persist coffee records in a SQLite database using EF Core code-first migrations. A coffee record SHALL include a name, roaster, origin, roast level, price, date bought, optional photo path, optional shop name, optional purchase URL, the id of the user who created it, and a creation timestamp.

#### Scenario: Database schema is created from migrations

- **WHEN** the `InitialCreate` migration is applied to a fresh database
- **THEN** a coffee table SHALL exist with columns for all coffee fields
- **AND** the database file SHALL be created on disk

#### Scenario: Identity and timestamp are assigned on insert

- **WHEN** a coffee record is saved
- **THEN** the system SHALL assign a unique identifier
- **AND** SHALL record a creation timestamp

### Requirement: Coffee catalog is readable over HTTP

The system SHALL expose `GET /api/coffees` returning the list of stored coffees serialized as response DTOs. The response SHALL NOT expose internal entity-only fields beyond what the DTO defines.

#### Scenario: Listing coffees when the catalog is empty

- **WHEN** a client sends `GET /api/coffees` and no coffees exist
- **THEN** the system SHALL respond with HTTP 200
- **AND** the body SHALL be an empty JSON array

#### Scenario: Listing coffees when records exist

- **WHEN** a client sends `GET /api/coffees` and one or more coffees exist
- **THEN** the system SHALL respond with HTTP 200
- **AND** the body SHALL be a JSON array of coffee DTOs, one per stored coffee

### Requirement: API exposes interactive OpenAPI documentation in development

The system SHALL serve Swagger/OpenAPI documentation in the Development environment so the catalog endpoint can be explored and exercised.

#### Scenario: Swagger UI is available in development

- **WHEN** the API runs in the Development environment and a developer opens the Swagger UI
- **THEN** the system SHALL list the `GET /api/coffees` operation
- **AND** the developer SHALL be able to invoke it and receive a response
