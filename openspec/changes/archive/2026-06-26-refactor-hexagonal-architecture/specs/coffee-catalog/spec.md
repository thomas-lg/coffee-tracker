## ADDED Requirements

### Requirement: API layer is decoupled from the persistence mechanism

The system SHALL keep the HTTP/API layer independent of the persistence mechanism. Controllers SHALL depend only on an application-layer port (a service abstraction) and SHALL NOT reference the database context or ORM directly. Data access SHALL be provided through a port whose implementation can be substituted without changing the API or application layers.

#### Scenario: Controller has no direct dependency on the database

- **WHEN** the API layer is built
- **THEN** no controller SHALL reference the EF Core `DbContext` (or any concrete persistence type)
- **AND** the catalog read path SHALL flow through an application service that depends on a repository port

#### Scenario: Persistence implementation is substitutable

- **WHEN** the application service is exercised with a substitute (fake/in-memory) implementation of the repository port
- **THEN** the catalog read behavior SHALL be testable without a database or EF Core
