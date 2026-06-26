## ADDED Requirements

### Requirement: Uploaded photos are validated before storage

The system SHALL accept only image uploads whose content type is on an allowlist (`image/jpeg`, `image/png`, `image/webp`) and whose size does not exceed a configurable maximum (default 5 MB). Uploads failing either check SHALL be rejected without being written to disk.

#### Scenario: Rejecting a disallowed content type

- **WHEN** a file whose content type is not on the allowlist is submitted for storage
- **THEN** the system SHALL reject it
- **AND** SHALL NOT write any file to disk

#### Scenario: Rejecting an oversized upload

- **WHEN** a file larger than the configured maximum is submitted for storage
- **THEN** the system SHALL reject it
- **AND** SHALL NOT write a partial or complete file to disk

### Requirement: Stored photos use server-generated names in a fixed directory

The system SHALL store each accepted photo under a configurable photos directory using a server-generated random filename, with the extension derived from the validated content type — never from the client-supplied filename. The storage layer SHALL return a relative path for persistence.

#### Scenario: A valid photo is stored with a safe name

- **WHEN** a valid image is stored
- **THEN** the saved filename SHALL be server-generated (not derived from the upload's filename)
- **AND** the returned path SHALL be relative to the photos directory
- **AND** the upload SHALL NOT be able to influence the storage path (no directory traversal)

### Requirement: Stored photos are served read-only over HTTP

The system SHALL serve files from the photos directory at the `/photos` request path as static content, without directory browsing.

#### Scenario: Loading a stored photo by URL

- **WHEN** a client requests a stored photo's `/photos/{name}` URL
- **THEN** the system SHALL return the image content
- **AND** SHALL NOT list the contents of the photos directory for a request to `/photos`
