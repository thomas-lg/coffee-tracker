## ADDED Requirements

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
