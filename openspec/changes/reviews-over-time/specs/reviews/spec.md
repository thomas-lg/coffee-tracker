## MODIFIED Requirements

### Requirement: A user can review a coffee

The system SHALL let an authenticated user create a review for a coffee via `POST /api/coffees/{coffeeId}/reviews`, capturing a rating (1–5), an optional stage/context label (e.g. "Fresh bag", "Last cups"), optional tasting notes, optional brew details (method, grind, ratio), and zero or more flavor tags. A user MAY create **multiple** reviews for the same coffee over time; each `POST` records a new, independently dated entry.

#### Scenario: Creating a review

- **WHEN** an authenticated user posts a valid review for a coffee
- **THEN** the system SHALL create the review owned by that user
- **AND** SHALL record its creation timestamp
- **AND** SHALL respond with the created review

#### Scenario: Rating the same coffee again over time

- **WHEN** a user who already has a review for a coffee posts another for the same coffee
- **THEN** the system SHALL create a second, separately dated review
- **AND** SHALL NOT reject it as a duplicate

#### Scenario: Recording an optional stage

- **WHEN** a review is posted with a stage label
- **THEN** the system SHALL persist that stage with the entry
- **AND** SHALL accept a review with no stage

#### Scenario: Rating out of range is rejected

- **WHEN** a review is submitted with a rating below 1 or above 5
- **THEN** the system SHALL respond with `400`
- **AND** SHALL NOT create a review

#### Scenario: Reviewing a missing coffee

- **WHEN** a user posts a review for a coffee id that does not exist
- **THEN** the system SHALL respond with `404`

### Requirement: Reviews for a coffee can be listed and read individually

The system SHALL return all reviews for a coffee via `GET /api/coffees/{coffeeId}/reviews`, ordered newest-first, and a single review via `GET /api/coffees/{coffeeId}/reviews/{id}`, each including its rating, optional stage, notes, brew details, flavor tags, owner, and timestamps. A successful create SHALL return a `Location` header pointing at the new review's single-review URL.

#### Scenario: Listing reviews newest-first

- **WHEN** an authenticated user requests the reviews for an existing coffee
- **THEN** the system SHALL respond with that coffee's reviews ordered newest-first

#### Scenario: Reading a single review

- **WHEN** an authenticated user requests an existing review by id under its coffee
- **THEN** the system SHALL respond with that review

#### Scenario: Listing reviews for a missing coffee

- **WHEN** a user requests reviews for a coffee id that does not exist
- **THEN** the system SHALL respond with `404`
