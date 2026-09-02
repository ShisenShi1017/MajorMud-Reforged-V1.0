# Changelog

This changelog is intentionally compact. Detailed historical pass-by-pass notes were retired during CodeUpdate0141; Git history remains the historical record.

## CodeUpdate0141 — Validated Client and Repository Consolidation

- Consolidated the active root documentation into `README.md`, `PROJECT_STATUS.md`, `CHANGELOG.md`, `VERSION.md`, and `AGENTS.md`.
- Promoted the repository metadata baseline from CodeUpdate0140 to CodeUpdate0141.
- Recorded the locally verified automated baseline: Reforged.App and Reforged.Server Release builds clean, current-health validator 26/26 PASS, and Reforged client regression smokes 14/14 PASS.
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
