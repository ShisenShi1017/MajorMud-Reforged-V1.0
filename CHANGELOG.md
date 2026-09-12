# Changelog

This changelog is intentionally compact. Detailed historical pass-by-pass notes were retired during CodeUpdate0141; Git history remains the historical record.

## CodeUpdate0142 — RealmGate Client and Character Integration

- Continued visible client/release rebranding to RealmGate without destructive namespace/project-folder renames.
- Added the bundled Classic route graph used by Goto/Auto-Roam and removed dangling route edges.
- Consolidated the Character page around Summary, Equipment Planner, Statistics/source breakdowns, Spells & Effects, and Progression while preserving effective server/BBS overlay authority.
- Hardened Borin persistent-character adoption and corrected parity-smoke/release target paths.
- RealmGate app Release build and full solution build pass with 0 warnings / 0 errors; bootstrap validator passes with 0 issues; all 14 client smokes pass; pushed GitHub Classic parity closure passes. CodeUpdate0142 is closed.
- Post-closure consolidation made every generated world manifest an enforced standalone-runtime contract and removed live derived-world permission to fall back to Classic.
- Corrected Custom-world MDB NPC projection so an absent group field no longer overwrites inherited group identity with zero; regenerated Custom1 resolves all 962 spawn records and materializes 1,015 live NPCs.
- Routed unregistered plain text directly to SAY, preserved direct world selection, and verified Custom1 living-world, economy, MegaMUD/SAY, save isolation, and no-Classic-runtime-fallback behavior.
- Updated decode exhaustion for the four enforced world-manifest contract fields; the exact baseline is now 48/48 families and 2,316/2,316 fields with zero disconnected or unresolved fields.
- Added the red-and-black beveled `RG` application icon, explicit RealmGate executable metadata, and an `asInvoker` Windows manifest.
- Added a fail-closed RealmGate release publisher that supports trusted certificate-store signing, SHA-256/RFC 3161 timestamping, and post-sign verification without storing private credentials in Git.
- Confirmed the current local executable is unsigned; trusted code-signing and exact-artifact SmartScreen testing remain required before claiming the antivirus/reputation warning resolved.
- Added account-owner character deletion under each selected world's character menu and a world-aware Creator Studio Characters tab; deletion validates ownership, realm identity, active-session safety, account-slot cleanup, and save removal.
- Moved the live server session list and runtime/application log view under Host, removed the obsolete separate Server navigation entry, and prevented Host UI command exceptions from closing RealmGate.
- Isolated generated-world load failures and Telnet session failures so one invalid custom world or character session does not stop healthy hosted worlds.
- Compiled decoded light sources into the existing `LIGHT` equipment channel and rendered their remaining charge readiness instead of a body slot.
- Verified the hardening with the 26/26 full validator, 14/14 client smokes, clean direct App/Server/validator Release builds, and targeted create/restore/LIGHT-equip/delete/account-cleanup coverage.

## CodeUpdate0141 — Validated Client and Repository Consolidation

- Consolidated the active root documentation into `README.md`, `PROJECT_STATUS.md`, `CHANGELOG.md`, `VERSION.md`, and `AGENTS.md`.
- Promoted the repository metadata baseline from CodeUpdate0140 to CodeUpdate0141.
- Recorded the locally verified automated baseline: RealmGate.App and RealmGate.Server Release builds clean, current-health validator 26/26 PASS, and Reforged client regression smokes 14/14 PASS.
- Integrated the completed Reforged client capability expansion and Creator Studio provenance architecture into the current project documentation.
- Corrected client smoke assertions that were colliding with unrelated valid automation and with the cleaned repository's intentionally absent local MegaMUD `.mp` evidence archive.
- Added generated-client-smoke report ignore rules.
- Kept real external-BBS MDB and Creator Studio MDB + matching WCC/WGS imports as explicit field-integration certification tasks.

## CodeUpdate0140 — Classic Parity Closure and Creator Studio

- Closed the accepted Classic parity ledger at 200 PASS / 0 STRONG / 0 UNKNOWN.
- Consolidated the single current-health validator and retained the cumulative Classic parity gate.
- Completed the Character/Status, Rooms/World, Timers/Resets/World Rules, quests/scripts/triggers, NPC/AI, combat, spells, economy/services, persistence, limits/recovery, and remaining native ownership closures.
- Established Librarium as committed gameplay authority and `Data/` as generated runtime output.
- Added Creator Studio Custom-world MDB handling and the MDB/WCC provenance direction.
- Archived imported/reference evidence as local-only material rather than a normal runtime dependency.
- Expanded Reforged client functionality toward full MajorMUD-client capability while keeping Reforged's independent WPF implementation.

## CodeUpdate0139 — Telnet Runtime Cohesion

- Consolidated the authoritative server/Telnet gameplay path and legacy-client compatibility.
- Integrated MegaMUD connection/profile behavior, terminal framing, login/character routing, and server-authoritative gameplay presentation.

## CodeUpdate0138 — Server Runtime and Host Enablement

- Established the locally playable server/runtime and Host foundation.

## CodeUpdate0137 — Complete Classic Gameplay Baseline

- Established the earlier complete Classic gameplay baseline that later parity passes verified and corrected.

Earlier implementation history is available through Git commits rather than duplicated root documentation.
