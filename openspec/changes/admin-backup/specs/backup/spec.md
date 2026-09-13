## ADDED Requirements

### Requirement: An administrator can export the whole catalog

The system SHALL expose `GET /api/admin/backup`, restricted to administrators, returning
every coffee with its reviews as a JSON document. The document SHALL carry a format
version, so a file can be recognised by an instance that cannot read it. Flavour tags
SHALL be carried by name rather than by id, because ids are per-instance. Record ids SHALL
NOT be carried at all. The response SHALL be marked as an attachment with a dated
filename.

#### Scenario: Exporting a catalog

- **WHEN** an administrator sends `GET /api/admin/backup`
- **THEN** the system SHALL respond with HTTP 200
- **AND** the body SHALL carry the current format version, an export timestamp, and every
  coffee with its reviews
- **AND** each review SHALL list its flavour tags by name
- **AND** the response SHALL set `Content-Disposition: attachment` with a filename carrying
  the export date

#### Scenario: Exporting an empty catalog

- **WHEN** an administrator exports an instance with no coffees
- **THEN** the system SHALL respond with HTTP 200 and an empty list of coffees

#### Scenario: Export is refused to anyone else

- **WHEN** a user who is not an administrator sends `GET /api/admin/backup`
- **THEN** the system SHALL respond with HTTP 403
- **AND** an unauthenticated request SHALL be refused with HTTP 401

### Requirement: An administrator can restore a catalog, replacing the current one

The system SHALL expose `POST /api/admin/backup`, restricted to administrators, which
replaces the entire catalog with the contents of the submitted document. Coffees and
reviews SHALL be written as new records. The write SHALL be atomic: either the whole
catalog is replaced or nothing is. Accounts, sessions and settings SHALL NOT be affected,
so a restore cannot grant anyone access.

#### Scenario: Restoring replaces rather than appends

- **WHEN** an administrator restores a document holding two coffees onto an instance that
  already holds coffees
- **THEN** the catalog SHALL afterwards hold exactly those two coffees
- **AND** reviews belonging to the replaced coffees SHALL NOT survive
- **AND** each restored review SHALL belong to the coffee it was exported under

#### Scenario: Restoring an empty document clears the catalog

- **WHEN** an administrator restores a document with no coffees
- **THEN** the catalog SHALL afterwards be empty

#### Scenario: The result reports what was written

- **WHEN** a restore succeeds
- **THEN** the system SHALL respond with HTTP 200
- **AND** the body SHALL report how many coffees and reviews were written, and any warnings

### Requirement: A document that cannot be trusted is refused before anything is written

The system SHALL validate the entire document before it writes anything, so a defect near
the end of a file cannot leave the instance holding neither the old catalog nor a whole
new one. A document whose format version the instance does not read SHALL be refused with
a reason naming both versions. A document whose contents are invalid SHALL be refused with
a reason identifying the offending record.

#### Scenario: A document from a newer instance is refused

- **WHEN** an administrator submits a document whose format version the instance does not
  read
- **THEN** the system SHALL respond with HTTP 400
- **AND** the reason SHALL name both the document's version and the version this instance
  reads
- **AND** the catalog SHALL be unchanged

#### Scenario: An invalid record anywhere refuses the whole document

- **WHEN** an administrator submits a document in which any coffee has no name, roaster or
  origin, an unknown roast level, a negative price, or a review rated outside 1 to 5
- **THEN** the system SHALL respond with HTTP 400
- **AND** the reason SHALL identify the offending coffee
- **AND** the catalog SHALL be unchanged, even when the offending record is the last one

### Requirement: Flavour tags are matched by name, never invented

Flavour tags are a closed set of seeded reference data. On restore, the system SHALL match
each tag name against the seeded tags, ignoring case. A name it does not recognise SHALL be
skipped and reported as a warning rather than created, because a tag outside the closed set
would appear on screen and match no filter. A restore SHALL NOT delete or alter the seeded
tags.

#### Scenario: A known tag is matched regardless of casing

- **WHEN** a restored review carries the tag name `BERRY` and the instance seeds `Berry`
- **THEN** the restored review SHALL carry the seeded `Berry` tag

#### Scenario: An unknown tag is skipped and reported

- **WHEN** a restored review carries a tag name the instance does not seed
- **THEN** the tag SHALL NOT be attached to the review
- **AND** the tag SHALL NOT be created
- **AND** the result SHALL carry a warning naming it

### Requirement: A backup carries data, not photos

The document SHALL carry each coffee's stored photo path but SHALL NOT carry the image
itself. Photos are ordinary files on the photo volume, which an operator can copy
independently.

#### Scenario: A restored coffee keeps its photo path

- **WHEN** a coffee with a photo is exported and restored onto the same instance
- **THEN** the restored coffee SHALL resolve the same photo
