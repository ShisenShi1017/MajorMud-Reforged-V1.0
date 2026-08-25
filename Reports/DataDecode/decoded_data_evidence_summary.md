# Decoded Data Evidence Summary

This report consolidates the remaining useful decoded-data conclusions from the historical report folders. It is a provenance and audit summary, not an authoritative runtime data source.

Authoritative gameplay data remains in `Librarium/` and generated runtime data remains in `Data/`. Imported/native files remain reference evidence until they are explicitly archived.

## Source Status

- Available local WCC/WBB families cover banks, classes, gangs, items, item-world ownership, known messages, map room text, messages, races, shops, spells, textblocks/quests, updates/validation rules, and users/character foundation.
- Supplied WCC files are exhausted for static decode purposes.
- Directly recoverable consumers in `MMUD.DLL`, `MMPLS.DLL`, and `MMUTL.EXE` have either been traced or placed behind a proven Worldgroup/native-host boundary.
- HSE/STO support files are exhausted as files, but exact loader/fallback behavior is not directly recoverable from the supplied static WCC module set.
- Reforged must not reopen exhausted WCC files to invent behavior. Remaining unknowns stay fail-closed unless new original artifacts or runtime proof are supplied.

## Room Description Closure

- Canonical room records checked: 26,666.
- WCCMP002 room-description recovery promoted 23,309 additional descriptions.
- After the first WCCMP002 pass, 140 placeholder-named rooms remained unresolved.
- The placeholder pass recovered all 140 remaining descriptions from decoded WCCMP002 native room-record offsets, with WBB title and first-line corroboration for all 140.
- Final room-description coverage reached 26,666/26,666 for the catalog covered by these reports.
- WCCMP002 is treated as the selected MajorMUD 1.11p room/map source for the placeholder pass. WBBMP002 is secondary comparison evidence.

## Room Actions, Effects, and Messages

- Gap exit records resolved: 12.
- Distinct native/passive effects resolved: 7.
- MegaMUD `/mp` route command findings for those gaps: 0.
- Native WCCMSG message records cataloged for this pass: 117.
- Hidden-action gaps are classified as native/passive room effects when MMExplorer exposes the hidden Needs Actions qualifier but no decoded Action row is available.
- No player command was invented for those passive/native effects.
- Message IDs use native WCCMSG byte offsets plus 512-byte block location instead of guessed internal enums.

## MMExplorer and OmniVerse Reconciliation

- MMExplorer SQL/MDB and OmniVerse public codex material are reference/correlation evidence only.
- They do not override binary-backed fields.
- Matching reference rows may be attached as provenance or structured supplements.
- Unmatched MMExplorer supplements are retained only as reference records until a canonical binary-backed or strongly corroborated promotion path exists.

## Items Boundary

- Items reached the native-only boundary at 1,950 canonical item records.
- Evidence-safe runtime coverage includes identity, ownership, transfer, stacking infrastructure, equipment, containers, charges and durability primitives, corpses, resurrection, banking, merchants, resets, NPC inventory and loot reconciliation, quest/event items, limited-item enforcement, persistence validation/recovery, cross-save coordination, safe repair, and administrative operations.
- Remaining item behavior depends on native record layouts, flags, restriction bits, stack limits, charge costs, durability formulas, loot RNG, reset scheduling, save layouts, exact messages, and native exception ordering.
- Unknown item behavior remains inactive or evidence-gated.

## Combined Subsystem Boundary

The major gameplay subsystem decode packages reached their pre-native boundary:

- Rooms
- Triggers, scripts, and quests
- Character
- Textblocks, commands, and abilities
- NPC and AI
- Combat and spells
- Economy
- Remaining systems
- Items

No additional broad non-native decode window is recorded as required before native/host closure. Remaining work belongs to native executable behavior, runtime proof, or newly supplied original artifacts.

## Remaining Native/Host Boundaries

The following are no longer ordinary WCC-file decode tasks and must be resolved through `WGSERVER.EXE`, matching runtime DLLs, symbols, dynamic traces, controlled save/database samples, or equivalent original proof:

- Authorization token generation and host collation.
- HSE/STO dynamic/external consumer behavior.
- RNG and seed lifecycle.
- Save/database serialization, checksums, locks, migration, journaling, and crash recovery.
- Scheduling, polling, message transport, formatting, callback targets, and exact host globals.
- Native spawn population algorithms, perception masks, hostility classification, threat/path selection, chase/leash behavior, and retry cadence.
- Exact formula ordering, random ranges, timing, status restart semantics, economy stage ordering, and native command/message exception behavior.

## Retired Report Classes

The following report classes are historical validation or checkpoint evidence after this consolidation:

- Bundle A/B/D/F install, build, smoke, and audit logs.
- Combined update merge reports.
- Numbered item semantic exhaustion reports.
- Final native closure update mini-reports.
- Historical top-level update reports already removed from the repository.

These are not runtime data sources. The retained runtime authority is Librarium plus generated Data.
