## ADDED Requirements

### Requirement: An administrator can audit stored photos for orphans

The system SHALL expose, to administrators only, a listing of every stored photo via
`GET /api/admin/photos`, each marked **used** (its path is referenced by a coffee) or
**unused** (orphaned — e.g. a scan whose coffee was never saved). Non-administrators
SHALL be refused with `403` and SHALL NOT receive the listing.

#### Scenario: Listing photos with usage

- **WHEN** an administrator requests `GET /api/admin/photos`
- **THEN** the system SHALL respond `200` with every stored photo's relative path
- **AND** each entry SHALL be marked used or unused according to whether a coffee references it

#### Scenario: A non-administrator is refused

- **WHEN** an authenticated non-administrator requests `GET /api/admin/photos`
- **THEN** the system SHALL respond `403`
- **AND** SHALL NOT return the photo listing

### Requirement: An administrator can delete selected unused photos

The system SHALL let an administrator delete specific stored photos by their relative
paths via `DELETE /api/admin/photos`. The system SHALL delete only requested paths
that are **unused** at delete time, skipping any a coffee still references, so cleanup
cannot strip a catalogued coffee of its photo. Deletion SHALL be best-effort and
idempotent (an already-missing file is not an error).

#### Scenario: Deleting orphaned photos

- **WHEN** an administrator requests deletion of one or more unused photo paths
- **THEN** the system SHALL delete those files
- **AND** SHALL report how many were deleted

#### Scenario: A still-referenced photo is skipped

- **WHEN** an administrator's delete request includes a path that a coffee currently references
- **THEN** the system SHALL retain that file
- **AND** SHALL NOT delete it, reporting it as skipped

#### Scenario: A non-administrator cannot delete

- **WHEN** an authenticated non-administrator requests `DELETE /api/admin/photos`
- **THEN** the system SHALL respond `403`
- **AND** SHALL NOT delete any file
