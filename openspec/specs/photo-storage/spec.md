# photo-storage Specification

## Purpose
TBD - created by archiving change m2-coffee-crud-and-photo-upload. Update Purpose after archive.
## Requirements
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

#### Scenario: Oversized uploads are refused at the request boundary

- **WHEN** a request body exceeds the configured maximum (plus a small framing allowance)
- **THEN** the server SHALL refuse the request before buffering the whole body
- **AND** SHALL NOT write any file to disk

### Requirement: Stored photos use server-generated names in a fixed directory

The system SHALL store each accepted photo under a configurable photos directory using a server-generated random filename, with the extension derived from the validated content type — never from the client-supplied filename. The storage layer SHALL return a relative path for persistence.

#### Scenario: A valid photo is stored with a safe name

- **WHEN** a valid image is stored
- **THEN** the saved filename SHALL be server-generated (not derived from the upload's filename)
- **AND** the returned path SHALL be relative to the photos directory
- **AND** the upload SHALL NOT be able to influence the storage path (no directory traversal)

### Requirement: Stored photos are served read-only over HTTP

The system SHALL serve files from the photos directory at the `/photos` request path as static content, without directory browsing. Responses SHALL include `X-Content-Type-Options: nosniff` so a stored file cannot be MIME-sniffed into active content.

#### Scenario: Loading a stored photo by URL

- **WHEN** a client requests a stored photo's `/photos/{name}` URL
- **THEN** the system SHALL return the image content
- **AND** the response SHALL carry `X-Content-Type-Options: nosniff`
- **AND** SHALL NOT list the contents of the photos directory for a request to `/photos`

### Requirement: Stored photos are cleaned up with their coffee

The system SHALL delete a stored photo file when it is replaced by a new upload or when its coffee is deleted, so unreferenced files do not accumulate. Deletion SHALL be best-effort: a missing file is not an error.

#### Scenario: Replacing a photo removes the previous file

- **WHEN** a coffee that already has a photo receives a new photo upload
- **THEN** the system SHALL store the new file
- **AND** SHALL delete the previously stored file

#### Scenario: Deleting a coffee removes its photo

- **WHEN** a coffee with a stored photo is deleted
- **THEN** the system SHALL delete the associated photo file

