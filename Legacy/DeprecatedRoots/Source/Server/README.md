# Source/Server

Standalone server implementation. Loads only from Reforged Data and never from ImportedDatabases during normal operation.

## Current Status

CodePatch 0002 creates the clean source location and architecture contract.
Implementation will be added in targeted code updates after this repository cleanup foundation.

## Dependency Rule

The end user should not need to install developer SDKs to run released builds. Source may exist here for development, but user-facing builds should be shipped as standalone executables in `Bin/Windows/Server/`.
