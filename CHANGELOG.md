# Changelog

- Added a repeatable Borin-free `-PublicRelease` publishing mode with isolated `Release\RealmGate-Public` output and public-safe executable metadata for Beta 0.1 distribution.

This changelog is intentionally compact. Detailed historical pass-by-pass notes were retired during CodeUpdate0141; Git history remains the historical record.

## CodeUpdate0142 — RealmGate Client and Character Integration

- Completed decoded Classic room-action and NPC-dialogue execution without converting dialogue into generic room actions. All 1,739 room-action aliases and 1,225 non-sentinel NPC keyword routes are executable; direct GREET routing covers 37 ownership links across all 19 source-authorized no-keyword roots. Added transactional PRICE debit, native CAST execution through the selected-world spell engine, client command visibility, hidden-exit enforcement, Ferryman two-way travel/save regression, and a zero-discrepancy source/generated audit. The 7,679-check regression and full 29/29 local validator passed with a 0-warning/0-error solution build; pushed CI has not been observed for this commit.
- Extended the local Avalonia Character planner with bounded selected-world Host item search, decoded details, and exact-ID read-only loadout comparisons. This does not add gameplay authority or external-BBS item data. Focused verification passed 72/72 client-core checks and core/server smokes; Avalonia Release build succeeded with two known analyzer-version warnings. Full validator and pushed CI have not been run for this slice.
- Local post-closure Avalonia Phase 3 Character slice: added a read-only selected-world character snapshot, Character summary/statistics/spells/effects/progress views, and reused inventory equipment-set planning. Focused verification passed 71/71 client-core checks and core/server smokes; Avalonia Release build succeeded with two known analyzer-version warnings. The full validator and pushed CI have not been run for this slice.
- Added the combined Avalonia assistant batch: shared recovery/combat/party planners and profiles, authoritative `AUTOMATIONSTATE` server messages, Assist views, supported communication commands, and live diagnostics. Included freshness, exact-room, character/world, learned-spell, repeat, disconnect, and Emergency Stop guards. Local verification passed 70/70 client-core checks and 28/28 full validator gates; Windows/Linux preview publishes completed. Native Linux GUI operation and pushed CI for this batch remain unverified.
- Included the core/server smoke project in the solution build so the validator cannot run stale server tests, and verified the automation snapshot with disposable character saves.
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
- Completed the next Avalonia Phase 2 navigation slice: remote hosts now deliver the selected world's generated graph in bounded compressed private frames with SHA-256 verification and no derived-world Classic fallback.
- Added world-qualified Favorite and Stash destinations to the Avalonia Navigation tab with exact-room admission and atomic local persistence.
- Expanded cross-platform client-core regression to 58/58, added a server end-to-end navigation-frame reconstruction smoke, and verified the full current-health gate at 28/28 with decode exhaustion at 49/49 families and 2,332/2,332 fields.
- Published and verified framework-dependent Avalonia artifacts for `win-x64` and `linux-x64`; both include the bundled Classic graph while remote derived-world graphs remain host-selected.
- Completed the Avalonia Phase 2 inventory slice with a structured selected-world/character server snapshot, per-character/world profile isolation, equipment-set capture/apply, preview-first rule execution, and fail-closed shop/bank/stash checks.
- Kept sell, stash, discard, buy, and currency deposit disabled by default and routed every approved action through the existing Emergency Stop, pacing, and ordinary server-command path.
- Expanded cross-platform client-core regression to 62/62 and added an end-to-end server smoke for the structured inventory frame; the full current-health validator remains 28/28 with Classic parity at 200/0/0.

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
