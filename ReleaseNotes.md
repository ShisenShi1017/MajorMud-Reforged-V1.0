# Release Notes

## Classic economy and service closure

All generated Classic shops, banks, trainers, inventory links, item economy records, and economy rules now have explicit runtime coverage. Live purchases apply decoded shop markup, and persistent restocking honors decoded timing, quantity, probability, and stock limits.

## Classic spell and status closure

All 1,350 generated Classic spells and 111 generated status effects now have explicit decoded execution-family coverage. All 105 decoded ability IDs are classified, damage/healing uses decoded ability vectors first, and the corpus audit reports zero unclassified records.

## Full decoded remoteaction closure

All 139 decoded textblock invocations (85 distinct signatures) now resolve to generated runtime contracts: 62 paired exit toggles, 22 persisted native state-bit controllers, and one common-finalization action. The contracts remain consolidated in `room_interactions.json`; no extra runtime data file was added. Local audit and living-world plus interaction/narrative smoke gates pass with zero evidence-gated signatures.

## Guardian Hall Engineering Department

Guardian Hall now includes Borin's ten-phase engineering department. The safe default is cumulative candidate ZIP generation inside isolated workspaces. Autonomous engineering and direct live-repository editing are independent, persisted, default-off operator controls.

Candidate ZIPs contain modified/new files only plus a patch, manifest, validation log, README, and `Install-and-Validate.bat`. Enabling autonomy alone does not grant live-edit authority.

Borin now exposes independent switches for planning, engineering, build, validation, runtime testing, gameplay testing, optimization, documentation, candidate ZIP generation, repository updates, and continuous overnight mode. Emergency Stop immediately cancels active execution and pauses queued work without changing those saved switches.

An Engineering Objective field and **Assign: Fix Runtime Disconnects** preset provide the missing problem assignment surface. The preset queues only the investigation and repair stages authorized by the enabled switches.

Autonomous Engineering now calls Borin's selected Forge/LM Studio coding model with captured diagnostic evidence, requires a unified Git diff, validates and applies it only inside the isolated worktree, and exposes the latest cumulative candidate ZIP path for next-day operator review.

## Guardian Hall Phase 3-0164 — Final Hardening and Self-Test

- Added atomic Guardian Hall state writes through a temporary file and replace operation.
- Added a recoverable `.backup` state snapshot before every replacement.
- Added automatic fallback to the backup when the primary state JSON is malformed.
- Advanced Guardian Hall persistence to schema version 7.
- Added a persisted diagnostics workspace and an in-application **Run Hall Self-Test** control.
- Added five-role smoke validation, Wanderer character retention, Worldsmith Live Realm isolation, Archivist preview-only enforcement, scheduler safety, connector URL/timeout bounds, and atomic save/reload verification.
- Added persisted pass/fail results to the Guardian Activity Center.
- Added Phase 3-0164 static validation and updated the Phase 3-0161 validator to accept later compatible schema versions.
- Release compilation remains pending in environments without the .NET 8 SDK.


## CodeUpdate0139

CodeUpdate0139 is a local Telnet runtime cohesion update.

Highlights:

- Keeps generated `Data/Classic` out of Git and regenerates it from Librarium during GitHub Actions before server bootstrap diagnostics.
- Adds chunked-record support for oversized Librarium source catalogs so committed authority data stays below GitHub file-size limits without promoting generated `/Data` to source.
- Keeps connected Telnet/MegaMUD sessions on the authoritative runtime dispatcher.
- Connects live combat targeting to visible living-world NPC instances.
- Synchronizes combat damage and spell/ability NPC effects with shared living-world state.
- Enforces the live combat movement guard before room traversal.
- Adds Classic runtime audit commands for compiler output, runtime load, room inspection, and spawn verification.
- Confirms regenerated `Data/Classic` now contains 26,668/26,668 room descriptions.
- Adds compiler-side native map room-text recovery diagnostics and export tooling.
- Promotes 2,417 binary-backed room descriptions into Librarium from clean available native map DAT evidence, including the required Newhaven/Arena/Guild probes.
- Promotes 800 lower-priority OmniVerse map-qualified reference room descriptions for canonical rooms not already covered by native evidence.
- Promotes 23,309 WCCMP002 default-install binary gap-fill descriptions, using the user-approved rule that duplicate same-title rooms may share one description.
- Promotes the final 140 WCCMP002 placeholder room descriptions, with WBBMP002 corroborating all recovered titles and first description lines. WCCMP002 is treated as the selected MajorMUD 1.11p room/map source for this pass.
- Updates compiler room-text projection to merge native and reference room-description files while keeping native/binary evidence first.
- Adds a read-only WCC Librarium exhaustion audit for available source coverage and remaining blockers.
- Reconciles MMExplorer SQL/MDB and OmniVerse public codex data into Librarium with explicit provenance and no binary-backed field overwrites.
- Adds unmatched MMExplorer supplements and an OmniVerse-backed abilities family.
- Regenerates live `Data/Classic` from Librarium so server audit reports 26,668 rooms with descriptions and 0 without descriptions.
- Centralizes command abbreviation parsing with exact aliases, unique-prefix resolution, and ambiguity rejection.
- Repairs live spawn placement so grouped spawn candidates cannot appear as a global room occupant list.
- Requires exact map-qualified spawn room resolution and logs/skips unresolved spawn records instead of falling back to arbitrary rooms.
- Adds displayed-entity placement proof to room audits and disposition-based NPC/monster coloring.
- Resolves the final 7 distinct native/passive room-effect linkage gaps across 12 exit rows without inventing player command bindings.
- Uses MegaMUD `/mp` route replay as path evidence for those gaps; replay found 0 command hits for the affected map-qualified exits.
- Adds 117 unique native WCCMSG offset-backed room message records for room actions, doors, hidden exits, locks/keys, searches, and failures.
- Loads the cumulative quest dialogue dispatch package into Librarium source records and regenerates Classic scripts/triggers from Librarium.
- Adds generated NPC dialogue dispatch for 182 decoded textblock stage scripts and 83 NPC quest-entry triggers.
- Connects `talk <npc>` to room-scoped live NPCs, decoded stage-order script evaluation, atomic character mutation, and fail-closed rollback.
- Executes generated random textblock tables with decoded independent 1..100 threshold semantics.
- Validates generated quest world-intent targets for cast, summon, teleport, and remoteaction.
- Applies quest teleport intents to the active character/session and materializes summoned generated NPCs into the living world.
- Records `remoteaction` into the living-world event journal with exact decoded metadata while keeping the unknown native mutation evidence-gated.
- Records committed room/object interactions as durable living-world events.
- Imports the final native closure/WCC decode archive into the repo as evidence and Librarium/reference material.
- Keeps new `Native*` registry source files active and buildable, and merges the user-confirmed server implementation slices into active runtime code with duplicate contracts consolidated.
- Activates verified/native and strongly-correlated Librarium sidecar records in executable generated data where evidence supports it.
- Merges active room-interaction sidecars during compilation and records multi-source family provenance in generated manifests.
- Blocks unresolved exit targets during compiler cross-family validation instead of allowing stale active traversal.
- Adds a readable read-only Librarium browser tab to the Reforged app with Weapons, Armor, Classes, Races, Items, Monsters, and Rooms categories, selected-tab search, name-only record selection, and curated game-facing stat details without evidence/source metadata.
- Reworks the Client play flow toward MegaMUD: the Play tab selects server/session profiles, gameplay opens in a dedicated black terminal window, and settings/tools are kept in menus or a separate tabbed Client Options window.
- Fixes compiler room-description catalog discovery so plain `*_records.json` Librarium room-description files are included in generated `Data/Classic`.
- Fixes runtime quest dialogue and item catalog numeric readers so generated integer-compatible strings no longer crash server smoke tests expecting numeric JSON values.
- Closes the Bundle F native presentation payload gap with Bundle G Revision 0002 evidence: verified no-output decoded text nodes are explicit, `message 2044` and `message 3290` resolve to exact WCCMSG payloads, and unknown message IDs still fail closed.
- Adds a remaining native decode gap report for room objects, unresolved room/spawn references, remoteaction mutation, remaining spell/status behavior, economy/service edges, and native persistence/Worldgroup host behavior. Classic quest repeatability is closed as non-repeatable per character.
- Expands quest smoke coverage to include edge opcode counts and exhaustive dispatch validation for all generated NPC quest-entry triggers.
- Keeps `Saves/` and `ImportedDatabases/` preserved.

Verification:

- Server Release build: PASS.
- Core/server smoke harness: PASS.
- Classic audit commands: PASS.
- Compiler room-text audit: PASS.
- Room-description reference recovery: PASS.
- Compiler smoke output with merged descriptions: PASS, 26,668/26,668 descriptions emitted.
- WCCMP002 placeholder room-description validation: PASS.
- Live gameplay parser smoke: PASS.
- Living-world spawn placement smoke: PASS.
- Focused `room.map1.21` audit: PASS; one neutral room-scoped NPC, no storm giant/woodelf/boss/group leakage.
- Compiler smoke: PASS, 99,366 records across 37 families.
- Reforged app Release build: PASS.
- Reforged app publish to `Release/MajorMudReforged/MajorMudReforged.exe`: PASS.
- Reforged app startup smoke after the MegaMUD-style terminal-window repair: PASS.
- Complete current-health validator: PASS 13/13.
- Classic compiler/runtime audits: PASS; 26,668/26,668 generated room descriptions and 26,668/26,668 runtime-loaded descriptions.
- Interaction/narrative smoke: PASS; compact runtime contains 83/83 executable quest entries, 182 decoded stage scripts, zero pending quest candidates, and exhaustive NPC quest-entry dispatch validation.
- Room native effect/message decode: PASS; 12/12 target exits resolved and 117 unique room message records generated.
- Quest dialogue dispatch: PASS; 182 decoded stage scripts, 83 NPC quest-entry triggers, 182 linked script references, item consume/reward transition, and failure/no-mutation behavior.
- Quest/script edge closure: PASS; random table execution, cast/summon/remoteaction coverage, world-intent target validation, Classic non-repeat quest guard, and exhaustive NPC quest-entry dispatch validation.
- Remoteaction and room/object event recording: PASS.
- Final native closure archive intake: PASS; server/test builds and full validator pass after import and runtime audit repair.
- Bundle G native payload closure: PASS; F10 native payload audit reports 0 disconnects, and F10 complete through F15 persistence audits pass.
- Native payload reproducibility: PASS; all 3,467 verified WCCTEXT2 payloads now survive a clean Librarium compile as one generated runtime file.
- Latest validator report: `Reports/RuntimeVerification/validator_latest.md`.

Known limitation:

- WBBMP002 remains comparison evidence only and must not override WCCMP002 1.11p room/map source records.
- Current generated evidence for `room.map1.21` resolves to `guardsman` (`npc.14`), not the user-expected `elite guardsman`; correct this only through a separate evidence-backed Librarium/data update.
- Current hard placement validation logs and skips two unresolved spawn records: `spawn.288.43` and `spawn.2306.213`.
- Native/passive effect repair does not claim exact player commands for the 7 repaired effects; command promotion remains blocked without direct evidence.
- `remoteaction` is validated and recorded as a host event, but its unknown native remote mutation is not guessed.
- Native closure implementation slices are active runtime behavior after merge/replace cleanup and local verification; remaining limitations are compile warnings and GitHub Actions pending after push.
- Reference-only evidence remains gated unless verified/native or strongly-correlated evidence supports runtime activation.
- Ground objects still audit as 0 at runtime while compiler output reports 288 objects; static room-object placement/materialization remains a native decode/runtime integration gap.
- Five room references and two spawn placements remain unresolved and fail closed instead of falling back to arbitrary rooms.

GitHub Actions is pending until CodeUpdate0139 is pushed and confirmed green.

## CodeUpdate0138

CodeUpdate0138 is closed and green by user report.

Highlights:

- Extends the server runtime and host enablement baseline.
- Adds host configuration, module readiness, lifecycle, scheduler bootstrap, and scheduler status command audit surfaces.
- Expands runtime smoke coverage for live gameplay, multiplayer, living world, combat, interaction/narrative, services/economy, and world systems.
- Promotes MMExplorer map-qualified movement evidence through Librarium records.
- Applies MMExplorer textblock quest-flow fix 0142, promoting 83 quest-flow records into Librarium quests.
- Keeps Librarium authoritative and `/Data` generated-only.
- Keeps `ImportedDatabases/` reference-only and preserves real `Saves/`.
- The complete runtime gate now includes the 0142 textblock quest validator.

Latest local report:


Verification:

- PASS 68/68 from `python Tools\validator.py`.

GitHub Actions was reported green by the user for CodeUpdate0138.

One-time validation proofs are recorded in ValidationManifest.json.

## Guardian Hall Phase 2

Guardian Hall now opens a distinct operational chamber for each Guardian. Realm Keeper provides global realm oversight; Wanderer retains character-based exploration; Analyst and Worldsmith are character-free; Archivist provides file intake, evidence classification, reports, Codex exports, and editable local-tool launching. All Librarium changes remain preview-only pending explicit approval. Forge and Borin are unchanged and remain separate.

## Guardian Hall Phase 3 Update 0150

Realm Keeper now displays live local-host evidence instead of placeholder telemetry. Guardian Hall refreshes generated world counts, selected generated record totals, save/session files, validation/build health, recent activity, and crash evidence when opened, every 30 seconds, and on demand. Remote host scopes remain configured but are not falsely reported as connected without a real host telemetry endpoint.

## Guardian Hall Phase 3-0151
The Wanderer Expedition Lodge now reads local save/session evidence and generated room data. The assigned character remains required, while missing live sessions are explicitly reported rather than simulated.

## Guardian Hall Phase 3-0152

The Analyst observatory now reads real generated data for the selected balance target. It reports measurable source coverage, record counts, existing report history, graph readiness, and a current historical baseline. Magnus remains fully character-free and all analysis is read-only.

## Guardian Hall Phase 3-0153
Worldsmith now produces approval-gated proposal artifacts and safe simulations from measured repository baselines while remaining isolated from live realms and character control.

## Guardian Hall Phase 3-0154

Brother Caelum's workbench now performs safe local evidence inspection instead of only queuing files. Supported evidence can be decoded individually or as a queue, hashed, structurally inspected, cross-referenced, saved as preview evidence, reported, and exported for Codex. Librarium remains untouched until a separate explicit approval workflow exists.

## Guardian Hall Phase 3-0155

Brother Caelum can now generate complete investigation reports and Codex handoff packages from decoded evidence. The editable local-tool registry validates user-supplied executable and working-directory paths before launch and records launch status without hardcoding MMExplorer, HxD, IDA, Ghidra, Visual Studio, VS Code, Notepad++, decoder, or analyzer locations. All outputs remain preview-only.

## Guardian Hall Phase 3-0156

Worldsmith now supports an explicit approve-then-apply workflow for Test, Development, and Offline Projection realms. Approval is bound to the exact proposal hash. Apply writes only to the Guardian Hall isolated staging workspace and records a rollback backup and JSON receipt. Live realms, generated runtime data, and Librarium content remain protected.

## Guardian Hall Phase 3-0157
Guardian Hall now imports existing Phase 1 generic assignments into the role-specific Phase 2/3 workspace model. Migration is one-time, backed up, receipt-driven, and role-safe.

## Guardian Hall Phase 3-0158
Guardian Hall now includes persistent scheduled orchestration and an activity center. Scheduled jobs enforce each Guardian's safety boundaries and recover interrupted work after restart.

## Guardian Hall Phase 3-0159 — Remote Realm Telemetry
Realm Keeper can now monitor explicitly configured LAN and Internet hosts through authenticated HTTP telemetry endpoints. Credentials are supplied through environment variables and are never stored in Guardian Hall state. Failed endpoints remain visible but are not counted as connected.

## Guardian Hall Phase 3-0160 — Live Wanderer Session Connector
Lyra Windstep can now read an explicitly configured live character session and, only when separately enabled and permitted, request one structured mission step. Session credentials remain outside persisted state and local save evidence remains available as a safe fallback.

## Guardian Hall Phase 3-0161 — Analyst Trends and Report Packages
Analyst scans now create bounded historical baselines, visible distribution bars, comparison deltas, and multi-format report packages without mutating gameplay or Librarium data.

## Guardian Hall Phase 3-0162 — Archivist Evidence Browser
Brother Caelum can now search decoded evidence by file identity, findings, confidence, hashes, and cross-reference text. Selecting a result expands its direct relationships and reverse references without changing evidence or Librarium data.

## Guardian Hall Phase 3-0163 — Chamber Visual Identity
Each Guardian room now has a unique parchment palette, accent border, and role-purpose badge for faster visual orientation.
CodeUpdate0139 now includes the complete decoded WCCMMUD SYSOP/GAMEOP command catalog, authenticated role enforcement, six safely executable native command roots, and explicit fail-closed reporting for commands still awaiting exact native evidence or an owning runtime subsystem.
