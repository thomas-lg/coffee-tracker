## ADDED Requirements

### Requirement: An administrator chooses the OCR engine at runtime

The system SHALL persist which OCR engine scanning uses, and SHALL let an administrator
change it without restarting the instance. The change SHALL apply to the next scan.

When no engine has been chosen, the system SHALL use the one named by configuration, so
an instance that is upgraded rather than installed keeps scanning as its deployment says.

#### Scenario: Reading the engine on an instance nobody has configured

- **WHEN** an administrator reads the scanning settings and no choice has been stored
- **THEN** the system SHALL report the engine named by configuration

#### Scenario: Choosing an engine

- **WHEN** an administrator selects a different engine
- **THEN** the system SHALL store it
- **AND** the next scan SHALL use it
- **AND** no restart SHALL be required

#### Scenario: A stored value the build does not recognise

- **WHEN** the stored engine is not one the build knows
- **THEN** the system SHALL fall back to the configured engine
- **AND** SHALL NOT disable scanning

#### Scenario: Changing the engine leaves the account policy alone

- **WHEN** an administrator changes the engine
- **THEN** whether app accounts may sign in or register SHALL be unchanged

### Requirement: The available engines are reported with their availability

The system SHALL report every engine the build knows, each marked with whether it can run
on this host, so a client can refuse a choice that would answer 503 to every scan.

#### Scenario: An engine the host cannot run

- **WHEN** an engine's dependencies are absent from the host
- **THEN** the system SHALL report that engine as unavailable

## MODIFIED Requirements

### Requirement: Low-confidence lines are treated as noise

The system SHALL ignore recognised lines whose confidence falls below a floor, so
background clutter cannot become a coffee's name. The floor SHALL be reported by the
engine that produced the read rather than fixed for the pipeline, because engines differ
in how they score: the same clutter scores under 50 on one engine and around 81 on
another.

#### Scenario: Clutter is dropped

- **WHEN** a read contains lines below the reporting engine's floor
- **THEN** those lines SHALL NOT contribute to any parsed field

#### Scenario: An engine that reports no floor

- **WHEN** a read carries no floor
- **THEN** the parser SHALL apply its own default
