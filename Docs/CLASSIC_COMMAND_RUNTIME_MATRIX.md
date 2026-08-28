# Classic Command Runtime Matrix

This document records the current command-authority boundary without duplicating the live registration table.

## Authoritative Sources

- Native documented inputs: `Data/Classic/native_player_commands.json`, generated from retained WCCMMHLP evidence.
- Built-in parser registrations and compatibility aliases: `Apps/Source/Reforged.Server/RuntimeCommandSystem.cs`.
- Runtime route ownership: `Apps/Source/Reforged.Server/RuntimeCommandRouting.cs`.
- Contextual room actions: `Data/Classic/commands.json`, generated from the Librarium.

## Current Counts

- 63 native-help documented player-command inputs.
- 3 explicitly documented native aliases: BS, BR, and BG.
- 158 built-in Reforged registrations and 58 exact parser aliases, including compatibility, diagnostics, administration, and client-protocol commands that do not receive native parity credit merely by existing.
- 25 generated contextual room-action records.

## Validation Boundary

`--smoke-classic-native-player-commands` proves every documented native input and explicit native alias resolves through the live parser. `RuntimeBundleFPhaseF11CommandAudit` verifies every built-in registration has one runtime route and help-usage entry.

Undocumented aliases, exact native minimum prefixes, ambiguity precedence, hidden dispatcher entries, and native argument-parser ordering remain unresolved. Unknown or unbound contextual actions fail closed and do not mutate runtime state.
