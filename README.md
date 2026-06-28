# MajorMud Reforged

MajorMud Reforged is being built as a standalone MajorMud server, editor, importer, launcher, and future MegaMud-inspired client.

## Current status

- **Project Version:** 0.1.0
- **Current Milestone:** Milestone 1 - Core Library
- **Current Code Update:** 0005 - Core Utilities and Validation Foundation
- **Current Database Version:** 0009 - Server-Ready Data Baseline

## What changed in Code Update 0005

- Continued Milestone 1 by adding concrete Core source foundations.
- Added shared configuration models.
- Added shared logging contracts and a simple file logger source.
- Added validation result/issue/severity models.
- Added version metadata model.
- Added JSON document envelope model.
- Added repository health report models.
- Updated project documentation and repository health notes.

## Current architecture

```text
Apps/       Application front ends: Launcher, Server, Editor, Importer, Client
Core/       Shared models, validation, logging, configuration, serialization, versioning
Data/       Reforged editable game database
Docs/       Project documentation and history
Tools/      Importer/decoder/build support tools
Reports/    Audit and validation reports
Updates/    Code Updates and Database Versions
Bin/        Future executable output folders
```

No executable is claimed in this update. No .NET SDK is required to apply it.
