## ADDED Requirements

### Requirement: An administrator can review and delete orphaned photos

The web client SHALL provide an administrator-only photo-cleanup screen that lists
every stored photo marked used or unused, lets the administrator select unused photos
and delete them, and reports how many were deleted versus skipped. The screen and its
navigation entry SHALL NOT be reachable by non-administrators.

#### Scenario: Only admins can reach the screen

- **WHEN** a non-administrator navigates to the admin photo route
- **THEN** the client SHALL redirect them away from it
- **AND** SHALL NOT show an admin navigation entry

#### Scenario: Reviewing and deleting orphans

- **WHEN** an administrator opens the photo-cleanup screen
- **THEN** the client SHALL show each stored photo flagged used or unused
- **AND** SHALL let the administrator select unused photos, delete them after a confirmation, report the deleted/skipped counts, and refresh the list
