## ADDED Requirements

### Requirement: An administrator can export and restore the catalog from the admin view

The admin view SHALL offer a **Backup** section that downloads the catalog as a JSON file
and restores one. Because a restore replaces the catalog, choosing a file SHALL only arm a
confirmation naming the file and how many coffees it holds; nothing SHALL be sent until the
confirmation is accepted. The chosen file SHALL be parsed and shape-checked in the browser,
so a file that is not a backup is reported without a round trip. When the API refuses a
restore, the client SHALL show the reason the API gave rather than a generic failure. After
a restore the client SHALL reload the catalog, and SHALL report how many records were
written along with any warnings.

#### Scenario: Downloading a backup

- **WHEN** an administrator opens the Backup section and asks for a download
- **THEN** the client SHALL fetch the export with the session's token
- **AND** SHALL hand the browser a file named for the export date

#### Scenario: Choosing a file arms a confirmation

- **WHEN** an administrator chooses a valid backup file
- **THEN** the client SHALL show a confirmation naming the file and the number of coffees
- **AND** SHALL send nothing until it is accepted
- **AND** cancelling SHALL discard the chosen file

#### Scenario: A file that is not a backup is refused in the browser

- **WHEN** an administrator chooses a file that is not JSON, or JSON that is not a backup
- **THEN** the client SHALL report it
- **AND** SHALL send nothing

#### Scenario: A refused restore shows the reason

- **WHEN** the API refuses a restore
- **THEN** the client SHALL show the reason the API gave

#### Scenario: A completed restore reports what changed

- **WHEN** a restore succeeds
- **THEN** the client SHALL show how many coffees and reviews were written
- **AND** SHALL list any warnings the restore reported
- **AND** SHALL reload the catalog, so the shelf reflects what was restored
