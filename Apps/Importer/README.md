# Importer

This application will use the shared `Core` contracts introduced in Code Update 0004.

Current state: scaffold/documentation only. No compiled executable is claimed by this update.

Future executable location:

`Bin/Windows/Importer/`

## Core Dependency

This application must use `Core/` for shared models, configuration, logging, validation, serialization, repositories, events, manifest access, versioning, health checks, and utilities.

Do not create app-local duplicate models for Core concepts. Compile-level Core references will be added when this application is promoted from placeholder/documented app folder to a buildable application project.


## Core Update 0011

A Milestone 1 source shell for this application now exists at `Apps/Source/Reforged.Importer/` and explicitly references Core. Runtime remains compiled-artifact based from `Bin/`.
