## ADDED Requirements

### Requirement: An administrator can choose the scanning engine from the admin view

The admin view SHALL offer a **Scanning** section listing every engine the build carries.
For each one it SHALL say what the engine is good at and what it costs, so the choice can
be made without reading the repository. It SHALL mark the engine in use, and SHALL NOT
allow selecting an engine the host cannot run.

#### Scenario: Reading the choice

- **WHEN** an administrator opens the Scanning section
- **THEN** the client SHALL show every engine with an explanation of its trade-offs
- **AND** SHALL mark which one is in use

#### Scenario: Changing the engine

- **WHEN** an administrator selects a different engine
- **THEN** the client SHALL apply the change through the API
- **AND** SHALL reflect the stored engine afterwards

#### Scenario: An engine this host cannot run

- **WHEN** an engine is reported unavailable
- **THEN** the client SHALL show it as not installed
- **AND** SHALL NOT allow selecting it

#### Scenario: A refused change

- **WHEN** the API refuses the change
- **THEN** the client SHALL report it
- **AND** SHALL continue to show the engine that is actually in use
