# Reforged Roadmap

- Evidence disposition is archive-only, never deletion. After 100% MajorMUD gameplay, board/Sysop/MajorMUD Plus, and MegaMUD emulation parity is proven, decoded/imported material not required by the live server will be packaged into a local server evidence ZIP and excluded from Git. Until then it remains available in place.
- The user-supplied MudPlay 3.18.8 archive is now an approved MIT-licensed implementation reference for MegaMUD automation and MMExplorer-style client browsing. Its Avalonia/.NET 10 UI is not embedded into the WPF/.NET 8 client; headless engines and tests are ported by capability and bound to authoritative generated Classic data under the acceptance gates in `Docs/Audits/MUDPLAY_CLIENT_PARITY_IMPORT_0140.md`.

This is the compact roadmap for the current repository. Historical update chains should not be recreated here.

## Current Milestone

- Current Code Update: `0139`
- Current Database Version: `0010`
- Current Milestone: `CodeUpdate0139 - Telnet Runtime Cohesion`
- Status: local runtime cohesion work applied; awaiting user push and GitHub Actions confirmation
- Verification: current-health validator PASS 13/13; Classic compiler/runtime audits PASS with 26,668/26,668 room descriptions; interaction/narrative smoke PASS with 83 executable quest entries; app Release build PASS; app publish PASS to `Release/MajorMudReforged`
- Guardian Hall engineering direction: Borin's phases 1-10 are permanent deterministic infrastructure with independent planning, engineering, build, validation, runtime/gameplay testing, optimization, documentation, candidate ZIP, repository-update, and overnight controls. Candidate ZIP generation defaults on; autonomous and repository-update controls default off.

## CodeUpdate0139 Active Scope

- Keep Telnet/MegaMUD sessions connected to the authoritative runtime.
- Use shared living-world NPC instances for room rendering, combat targeting, spell/ability effects, death, and respawn.
- Add diagnostics to prove where Classic data disappears before gameplay fixes continue.
- Current audit result: regenerated `Data/Classic` now emits 26,668/26,668 room descriptions.
- Current runtime audit result: Classic runtime loads 26,668 room descriptions, 1,015 live NPC instances, 628 live monster instances, 1,648 doors, 178 shops, 27 banks, 44 trainers, 83 executable quests, 83 live quest-entry triggers, and 182 executable stage scripts.
- Current recovery result: Librarium contains 2,417 clean native-backed room descriptions, 800 lower-priority OmniVerse map-qualified reference gap fills, 23,309 WCCMP002 default-install binary gap fills, and 140 WCCMP002 placeholder room recoveries. WCCMP002 is treated as the selected MajorMUD 1.11p room/map source for this pass; WBBMP002 is secondary comparison only.
- Current decode audit: `Reports/DataDecode/wcc_librarium_exhaustion_audit.md` inventories available WCC/WBB evidence and active Librarium coverage.
- Current reference reconciliation: `Reports/DataDecode/mmexplorer_omniverse_librarium_reconciliation.md` records MMExplorer SQL/MDB and OmniVerse public codex gap-fill promotion into Librarium.
- Current room-description reference recovery: `Reports/DataDecode/room_description_reference_recovery.md`.
- Current WCCMP002 room-description recovery: `Reports/DataDecode/wccmp002_room_description_recovery.md`.
- Current WCCMP002 placeholder room-description recovery: `Reports/DataDecode/wccmp002_placeholder_room_description_recovery.md`.
- Centralize command abbreviation handling so Telnet/MegaMUD input resolves through exact aliases, unique prefixes, and ambiguity rejection instead of socket-loop aliases.
- Current spawn placement repair: live NPC materialization requires exact map-qualified room resolution, skips unresolved spawns, avoids grouped-candidate leakage, and renders visible entities by runtime disposition.
- Current focused audit: `room.map1.21` renders one neutral `guardsman` from `spawn.21.5`; the user-expected `elite guardsman` is a separate evidence-backed data correction if the generated source is wrong.
- Current folded data scope: user-provided room audit/semantic and trigger-script-quest material is accepted into the current baseline and recorded in ValidationManifest.json.
- Current generated semantic counts: 26,668 rooms, 61,976 exits, 397 room interactions, 83 live NPC quest-entry triggers, 182 executable stage scripts, and 83 executable quests; 2,100 pending candidates and 284 duplicate room/exit analysis triggers remain Librarium-only.
- Current room decode closure: the final 7 distinct native/passive room-effect linkage gaps across 12 exit rows are resolved as native/passive effects, with MegaMUD `/mp` replay confirming 0 command hits and no invented player commands.
- Current room message decode: 117 unique WCCMSG offset-backed room message records are cataloged for room actions, doors, hidden exits, locks/keys, searches, and failures.
- Current quest/dialogue runtime slice: 182 decoded textblock stage scripts and 83 NPC quest-entry triggers are generated from Librarium, and `talk <npc>` now dispatches to decoded NPC dialogue with atomic condition/effect execution and fail-closed rollback.
- Current quest/script edge closure: generated random textblock tables execute through decoded 1..100 threshold bindings; cast/summon/teleport/remoteaction targets validate against generated data; quest teleports update the active session/character; summon intents materialize generated NPCs; remoteaction and room/object commits are recorded as living-world events; Classic completed quests cannot repeat on the same character; exhaustive NPC quest-entry dispatch validation passes.
- Current final native closure/WCC decode intake: archive evidence and Librarium/reference material are imported; active `Native*` registry files compile; non-integrated implementation slices remain reference-only under `Reference/FinalNativeClosure/`.
- Current verified/strong activation: strong Librarium sidecars are compiled into generated runtime data where evidence supports activation; unresolved generated exit targets are blocked by compiler cross-family validation.
- Current Bundle G closure: native NPC-dialogue presentation references are now resolved for the known Bundle F gap. Verified no-output decoded text nodes are explicit evidence-mapped, `message 2044` and `message 3290` resolve through exact WCCMSG payload evidence, and F10 native payload through F15 persistence audits pass locally.
- Native-presentation reproducibility is closed: the 3,467-record WCCTEXT2 authority is a canonical Librarium family and clean compilation regenerates its single runtime file.
- Current spell/status closure: all 1,350 generated Classic spells, 105 decoded ability IDs, and 111 generated status effects have explicit execution-family/runtime-consumer classifications; the corpus audit has zero unclassified records.
- Current economy/service closure: all 178 shops, 27 banks, 44 trainers, 1,204 inventory relationships, 1,950 item economy records, and 23 economy rules are classified; decoded markup and regeneration fields are active with zero unclassified services.
- Current remaining decode target: native presentation ordering where still opaque and persistence/host recovery equivalence are tracked in `Reports/RuntimeVerification/native_runtime_remaining_decode_gaps.md`. Static placement, remoteactions, spell/status, and economy/service corpus closure are complete locally.
- Current app shell update: the Librarium workspace is an active read-only Classic data browser with category tabs for weapons, armor, spells, abilities, classes, races, items, monsters, quests, and rooms; search; name-only record lists; character-foundation class/race gameplay details; nontechnical quest guidance; and readable dark parchment text.
- Current app client update: the Play workspace should remain a MegaMUD-style server/session launcher, with actual gameplay in a dedicated terminal window and settings/tools kept behind menus or a separate tabbed options window.
- Preserve the Librarium -> generated `/Data` -> runtime boundary.
- GitHub Actions and clean local validation regenerate generated `Data/Classic` from Librarium before server bootstrap; generated `/Data` is not source-controlled.
- Keep Classic evidence-locked and avoid custom/editor/multiplayer expansion beyond the approved server runtime path.

## Architecture Direction

- Librarium is authoritative.
- `/Data` is generated runtime output.
- Runtime consumes generated `/Data`.
- Classic is immutable and evidence-locked.
- Custom and AI worlds use overlays.
- Client is read-only for gameplay state.
- Host/server is authoritative.
- Imported/reference material remains untouched unless the user explicitly approves cleanup.

## Server Direction

Build the server as a reusable multi-world host, not a Classic-only executable.

Classic is the first target because it is the verified baseline. The design must stay ready for:

- Classic world
- Reforged Custom worlds
- Pure AI worlds
- Test realms
- Future modules

## Planned Public Updates

- `0139`: Telnet/MegaMUD runtime cohesion on top of the authoritative server runtime.
- `0140`: Custom/AI overlay-world support.
- `0141`: Host editor.

Numbers may shift only if the user explicitly approves a renumbering.

## Documentation Policy

Keep active documentation compact. Current continuity belongs in:

- `Docs/PROJECT_STATUS.md`
- this roadmap
- root continuity docs and manifests

Do not restore large historical doc trees unless the user asks.
### WCCMMUD SYSOP parity continuation

The generated authenticated command catalog and safe dispatch boundary are complete. Next recover exact invasion syntax/payload and GOTO room targets, then implement remaining command-owner services. Native gang commands require a data-backed MajorMUD gang runtime; do not substitute a generic guild subsystem.
Latest local continuation: grouped ability audit 123-124 is complete. HPRegen reuses the authoritative cadence; NegateAbility now suppresses only its evidence-backed target ability for spell and NPC owners, with zero-valued evidence kept fail-closed.
Latest local continuation: grouped ability audit 125-134 is complete. The ten consecutive quest-progress fields are canonical persistent character state, not combat or command abilities; runtime regression enforces both persistence and the non-power boundary.
