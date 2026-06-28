# CodeUpdate-0013 - Duplicate Model Enforcement

## Purpose

Core Update 0013 continues Milestone 1 by turning the duplicate-model rule into an enforceable repository health check.

Milestone 1 requires that shared data models are not duplicated across applications. Application-specific shells may exist, but model-like records, DTOs, entities, manifests, and shared result objects must live in `Core/` and be referenced by the application layer.

## Added

- `Core/Health/DuplicateModelEnforcer.cs`
- `Core/Health/DuplicateModelEnforcementIssue.cs`
- `Core/Health/DuplicateModelEnforcementReport.cs`
- `Core/Source/Reforged.Core/Health/DuplicateModelEnforcementHealthCheck.cs`

## Enforcement Behavior

The checker scans application source for model-like type names and compares them against Core declarations. If an application declares a model-like type that already exists in Core, the checker reports a Milestone 1 violation.

Model-like names include common shared-data suffixes such as:

- `Record`
- `Model`
- `Entity`
- `Dto`
- `Data`
- `Info`
- `Reference`
- `Manifest`
- `Configuration`
- `Result`
- `Issue`

Application bootstrap classes such as `ServerApplication`, `ImporterApplication`, `EditorApplication`, `LauncherApplication`, and `ClientApplication` are allowed because they represent app entry shells, not duplicated shared data models.

## Milestone 1 Effect

This update moves the `No duplicated models across applications` completion criterion from manual review toward automated enforcement.

Milestone 1 is not complete yet. Remaining recommended work is:

1. Expand Data JSON validation into cross-reference checks.
2. Add repository-backed loaders for each current Data category.
3. Prepare compiled/runtime-ready `Bin/` artifact expectations without requiring SDK use by normal users.
4. Create the final Milestone 1 verification report once all checklist items are satisfied.
