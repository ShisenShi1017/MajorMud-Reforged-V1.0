# Source/Editor

Editor implementation. Future editor should use server/editor APIs for live-safe changes instead of directly mutating running state.

## Current Status

CodePatch 0002 creates the clean source location and architecture contract.
Implementation will be added in targeted code updates after this repository cleanup foundation.

## Dependency Rule

The end user should not need to install developer SDKs to run released builds. Source may exist here for development, but user-facing builds should be shipped as standalone executables in `Bin/Windows/Editor/`.
