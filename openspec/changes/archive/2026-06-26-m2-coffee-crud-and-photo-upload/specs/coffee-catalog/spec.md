## ADDED Requirements

### Requirement: Coffees can be created over HTTP

The system SHALL expose `POST /api/coffees` accepting a coffee-create payload (name, roaster, origin, roast level, price, date bought, and optional shop name and purchase URL). The system SHALL validate the payload, assign an identifier and creation timestamp, persist the coffee, and return the created resource.

#### Scenario: Creating a coffee with a valid payload

- **WHEN** a client sends `POST /api/coffees` with a valid payload
- **THEN** the system SHALL respond with HTTP 201
- **AND** the response SHALL include a `Location` header pointing at the new coffee
- **AND** the body SHALL be the created coffee DTO with a server-assigned id and creation timestamp

#### Scenario: Rejecting an invalid create payload

- **WHEN** a client sends `POST /api/coffees` missing a required field or with a malformed purchase URL or negative price
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

The system SHALL expose `PUT /api/coffees/{id}` accepting a coffee-update payload, validating it and replacing the mutable fields of the identified coffee.

#### Scenario: Updating an existing coffee

- **WHEN** a client sends `PUT /api/coffees/{id}` with a valid payload for a coffee that exists
- **THEN** the system SHALL persist the updated fields
- **AND** SHALL respond with HTTP 204

#### Scenario: Updating a missing coffee

- **WHEN** a client sends `PUT /api/coffees/{id}` for an id that does not exist
- **THEN** the system SHALL respond with HTTP 404

### Requirement: Coffees can be deleted over HTTP

The system SHALL expose `DELETE /api/coffees/{id}` removing the identified coffee.

#### Scenario: Deleting an existing coffee

- **WHEN** a client sends `DELETE /api/coffees/{id}` for a coffee that exists
- **THEN** the system SHALL remove the coffee
- **AND** SHALL respond with HTTP 204

#### Scenario: Deleting a missing coffee

- **WHEN** a client sends `DELETE /api/coffees/{id}` for an id that does not exist
- **THEN** the system SHALL respond with HTTP 404

### Requirement: A photo can be attached to a coffee

The system SHALL expose `POST /api/coffees/{id}/photo` accepting a multipart image upload, storing it via the photo-storage capability and recording the returned relative path on the coffee's photo path.

#### Scenario: Attaching a valid photo

- **WHEN** a client uploads a valid image to `POST /api/coffees/{id}/photo` for a coffee that exists
- **THEN** the system SHALL store the image
- **AND** SHALL set the coffee's photo path to the stored relative path
- **AND** SHALL respond with HTTP 200 and the updated coffee DTO

#### Scenario: Attaching a photo to a missing coffee

- **WHEN** a client uploads an image to `POST /api/coffees/{id}/photo` for an id that does not exist
- **THEN** the system SHALL respond with HTTP 404
- **AND** SHALL NOT store the image

#### Scenario: Rejecting an invalid photo

- **WHEN** a client uploads a file whose content type is not an allowed image type, or whose size exceeds the configured cap
- **THEN** the system SHALL respond with HTTP 400
- **AND** SHALL NOT set the coffee's photo path
