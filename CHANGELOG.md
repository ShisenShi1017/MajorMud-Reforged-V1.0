# Changelog

## CodeUpdate-0007 - Core Contracts Foundation

- Rebuilt Core Update 0007 from scratch.
- Added Core event contracts, publisher/subscriber interfaces, and data change event model.
- Added service registry contracts.
- Added validation pipeline interface and dependency-light implementation shell.
- Added repository interfaces for editable data and world data access.
- Added provenance tracking contracts.
- Added runtime state model and server-ready world model interface.
- Added runtime execution policy: normal project runtime must not require the .NET SDK.
- Added update execution policy: update package must run from its own extracted folder and apply into the main project folder.
- Preserves `Saves/` and `ImportedDatabases/`; neither folder is included in this package.
