# Reforged Architecture

## Dependency Direction

All applications depend on Core. Core does not depend on applications.

Allowed direction:

`Apps/* -> Core -> Data contracts / shared utilities`

Forbidden direction:

`Core -> Apps/*`

## Core Responsibilities

Core owns shared contracts and reusable infrastructure:

- configuration
- logging
- validation
- serialization
- repositories
- shared models
- events
- service registry
- versioning
- manifest reading
- repository health checks
- utility helpers

## Application Responsibilities

- Server owns game runtime and networking.
- Importer owns native import pipelines and audit reports.
- Editor owns live editing workflows.
- Launcher owns routine startup, updates, backup, restore, logs, and validation.
- Client owns player-facing UI and network client features.

## Data Ownership

`Data/` is authoritative after import. `ImportedDatabases/` remains a preserved reference source and must not be used as the runtime database.

## Runtime Policy

User runtime must not require the .NET SDK. Applications should launch from `Bin/` compiled/runtime-ready artifacts.
