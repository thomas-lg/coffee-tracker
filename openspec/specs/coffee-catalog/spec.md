# coffee-catalog Specification

## Purpose
TBD - created by archiving change m1-backend-skeleton. Update Purpose after archive.
## Requirements
### Requirement: Coffee records are persisted in a database

The system SHALL persist coffee records in a SQLite database using EF Core code-first migrations. A coffee record SHALL include a name, roaster, origin, roast level, price, date bought, optional photo path, optional shop name, optional purchase URL, the id of the user who created it, and a creation timestamp. The database SHALL run in Write-Ahead Logging (WAL) journal mode so reads can proceed concurrently with a write.

#### Scenario: Database schema is created from migrations

- **WHEN** the `InitialCreate` migration is applied to a fresh database
- **THEN** a coffee table SHALL exist with columns for all coffee fields
- **AND** the database file SHALL be created on disk

#### Scenario: Database is configured for Write-Ahead Logging

- **WHEN** the application initializes the database on startup
- **THEN** the SQLite `journal_mode` SHALL be `wal`
- **AND** the setting SHALL persist in the database file across restarts

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

### Requirement: API layer is decoupled from the persistence mechanism

The system SHALL keep the HTTP/API layer independent of the persistence mechanism. Controllers SHALL depend only on an application-layer port (a service abstraction) and SHALL NOT reference the database context or ORM directly. Data access SHALL be provided through a port whose implementation can be substituted without changing the API or application layers.

#### Scenario: Controller has no direct dependency on the database

- **WHEN** the API layer is built
- **THEN** no controller SHALL reference the EF Core `DbContext` (or any concrete persistence type)
- **AND** the catalog read path SHALL flow through an application service that depends on a repository port

#### Scenario: Persistence implementation is substitutable

- **WHEN** the application service is exercised with a substitute (fake/in-memory) implementation of the repository port
- **THEN** the catalog read behavior SHALL be testable without a database or EF Core

### Requirement: Coffees can be created over HTTP

The system SHALL expose `POST /api/coffees` accepting a coffee-create payload (name, roaster, origin, roast level, price, date bought, and optional shop name and purchase URL). The system SHALL validate the payload, assign an identifier and creation timestamp, persist the coffee, and return the created resource.

#### Scenario: Creating a coffee with a valid payload

- **WHEN** a client sends `POST /api/coffees` with a valid payload
- **THEN** the system SHALL respond with HTTP 201
- **AND** the response SHALL include a `Location` header pointing at the new coffee
- **AND** the body SHALL be the created coffee DTO with a server-assigned id and creation timestamp

#### Scenario: Rejecting an invalid create payload

- **WHEN** a client sends `POST /api/coffees` missing a required field, with a malformed purchase URL or negative price, or with a `date bought` in the future
- **THEN** the system SHALL respond with HTTP 400
- **AND** no coffee SHALL be persisted

### Requirement: A single coffee can be read over HTTP

The system SHALL expose `GET /api/coffees/{id}` returning one coffee as a response DTO.

#### Scenario: Reading an existing coffee

- **WHEN** a client sends `GET /api/coffees/{id}` for a coffee that exists
- **THEN** the system SHALL respond with HTTP 200 and the coffee DTO

#### Scenario: Reading a missing coffee

- **WHEN** a client sends `GET /api/coffees/{id}` for an id that does not exist
- **THEN** the system SHALL respond with HTTP 404

### Requirement: Coffees can be updated over HTTP

The system SHALL expose `PUT /api/coffees/{id}` accepting a coffee-update payload, validating it and replacing the mutable fields of the identified coffee. Updates are restricted to the coffee's creator or an administrator (see "Catalog writes are restricted to the creator or an admin").

#### Scenario: Updating an existing coffee

- **WHEN** the coffee's creator (or an admin) sends `PUT /api/coffees/{id}` with a valid payload for a coffee that exists
- **THEN** the system SHALL persist the updated fields
- **AND** SHALL respond with HTTP 204

#### Scenario: Updating a missing coffee

- **WHEN** a client sends `PUT /api/coffees/{id}` for an id that does not exist
- **THEN** the system SHALL respond with HTTP 404

#### Scenario: Updating a coffee the caller does not own

- **WHEN** an authenticated non-admin user sends `PUT /api/coffees/{id}` for a coffee created by a different user
- **THEN** the system SHALL respond with HTTP 403
- **AND** SHALL NOT modify the coffee

### Requirement: Coffees can be deleted over HTTP

The system SHALL expose `DELETE /api/coffees/{id}` removing the identified coffee. Deletion is restricted to the coffee's creator or an administrator (see "Catalog writes are restricted to the creator or an admin").

#### Scenario: Deleting an existing coffee

- **WHEN** the coffee's creator (or an admin) sends `DELETE /api/coffees/{id}` for a coffee that exists
- **THEN** the system SHALL remove the coffee
- **AND** SHALL respond with HTTP 204

#### Scenario: Deleting a missing coffee

- **WHEN** a client sends `DELETE /api/coffees/{id}` for an id that does not exist
- **THEN** the system SHALL respond with HTTP 404

#### Scenario: Deleting a coffee the caller does not own

- **WHEN** an authenticated non-admin user sends `DELETE /api/coffees/{id}` for a coffee created by a different user
- **THEN** the system SHALL respond with HTTP 403
- **AND** SHALL NOT remove the coffee

### Requirement: A photo can be attached to a coffee

The system SHALL expose `POST /api/coffees/{id}/photo` accepting a multipart image upload, storing it via the photo-storage capability and recording the returned relative path on the coffee's photo path. Attaching a photo is restricted to the coffee's creator or an administrator (see "Catalog writes are restricted to the creator or an admin").

#### Scenario: Attaching a valid photo

- **WHEN** the coffee's creator (or an admin) uploads a valid image to `POST /api/coffees/{id}/photo` for a coffee that exists
- **THEN** the system SHALL store the image
- **AND** SHALL set the coffee's photo path to the stored relative path
- **AND** SHALL respond with HTTP 200 and the updated coffee DTO

#### Scenario: Attaching a photo to a missing coffee

- **WHEN** a client uploads an image to `POST /api/coffees/{id}/photo` for an id that does not exist
- **THEN** the system SHALL respond with HTTP 404
- **AND** SHALL NOT store the image

#### Scenario: Attaching a photo to a coffee the caller does not own

- **WHEN** an authenticated non-admin user uploads an image to `POST /api/coffees/{id}/photo` for a coffee created by a different user
- **THEN** the system SHALL respond with HTTP 403
- **AND** SHALL NOT store the image or modify the coffee

#### Scenario: Rejecting an invalid photo

- **WHEN** a client uploads a file whose content type is not an allowed image type, whose bytes do not match the declared image type, or whose size exceeds the configured cap
- **THEN** the system SHALL respond with HTTP 400
- **AND** SHALL NOT set the coffee's photo path

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

When a coffee is created by an authenticated user, the system SHALL record that user's id as the coffee's creator. Any authenticated user MAY create a coffee (the catalog is a shared shelf); ownership governs later modification, not creation.

#### Scenario: Creator id is stamped on create

- **WHEN** an authenticated user creates a coffee
- **THEN** the stored coffee's `CreatedByUserId` SHALL be the authenticated user's id

### Requirement: Catalog writes are restricted to the creator or an admin

Modifying a coffee — update (`PUT`), delete (`DELETE`), and photo attach (`POST /{id}/photo`) — SHALL be permitted only for the user who created it or for an administrator. A non-owner, non-admin caller SHALL receive HTTP 403 and the coffee SHALL be left unchanged. A coffee with no recorded creator (rows created before owner-stamping, `CreatedByUserId` is null) SHALL be modifiable by administrators only — never by an arbitrary authenticated user.

#### Scenario: Owner modifies their own coffee

- **WHEN** the user who created a coffee updates, deletes, or sets a photo on it
- **THEN** the system SHALL perform the operation

#### Scenario: Admin moderates another user's coffee

- **WHEN** an administrator updates, deletes, or sets a photo on a coffee created by a different user
- **THEN** the system SHALL perform the operation

#### Scenario: Non-owner is forbidden

- **WHEN** an authenticated non-admin user attempts to update, delete, or set a photo on a coffee they did not create
- **THEN** the system SHALL respond with HTTP 403
- **AND** SHALL leave the coffee unchanged

#### Scenario: Unowned legacy coffee is admin-only

- **WHEN** a non-admin user attempts to modify a coffee whose `CreatedByUserId` is null
- **THEN** the system SHALL respond with HTTP 403

### Requirement: Coffee responses include aggregate review stats

Coffee responses (both the list and single-coffee reads) SHALL include the coffee's average rating and the number of reviews. The average SHALL be null when the coffee has no reviews. These aggregates SHALL be computed without issuing a separate query per coffee.

#### Scenario: Coffee with reviews reports its average and count

- **WHEN** a coffee has one or more reviews and a client reads it (in the list or by id)
- **THEN** the response SHALL include the mean of its review ratings as `AverageRating`
- **AND** SHALL include the number of reviews as `ReviewCount`

#### Scenario: Coffee with no reviews

- **WHEN** a coffee has no reviews
- **THEN** `AverageRating` SHALL be null
- **AND** `ReviewCount` SHALL be 0

