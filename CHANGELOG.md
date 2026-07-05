# Changelog

## CodeUpdate-0065 - MMExplorer MDB Canonical Repair

- Recorded CodeUpdate-0064 GitHub Actions as green by user report.
- Extended the repository MDB extractor so it can emit owner-table summaries and selected owner-table raw rows from `ImportedDatabases/data-v1.11n.mdb`.
- Added `Data/StockGameRaw/MMExplorerMDB/` as the current MMExplorer MDB raw stock data surface for direct repair and follow-up decoding.
- Added `Tools/MDBExtractor/promote_mdb_rooms.py` to promote direct MDB-backed room fields reproducibly.
- Added canonical room `wcc_map1_room2150` as `Newhaven, Arena`.
- Added canonical room `wcc_map1_room2156` as `Small Cavern`.
- Added both rooms to `Data/world.json`, raising canonical/world room count from `1246` to `1248`.
- Updated smoke-test expectations for the repaired world projection count.
- Recorded owner table `1664` as the room/world row surface and owner tables `322`, `2566`, `15`, and `1660` as next repair candidates for monsters/NPCs, shops, classes, and races.
- No gameplay, command execution, scheduler execution, sockets, sessions, movement execution, combat, AI, live editor behavior, runtime formula/timing promotion, imported/reference deletion, or unknown byte-field promotion is included.

## CodeUpdate-0064 - Guarded Scheduler Status Command Surface

- Recorded CodeUpdate-0063 GitHub Actions as green by user report.
- Added guarded scheduler/status command diagnostics to Server bootstrap.
- Registered seven diagnostic commands: `status.overview`, `status.lifecycle`, `status.modules`, `status.scheduler`, `status.scheduler.channels`, `status.data`, and `status.validation`.
- Kept all status commands guarded, runtime-disabled, and non-mutating.
- Preserved ServerRuntimeValidation0013 movement/follow evidence under `Docs/ServerSim/ServerRuntimeValidation0013/`.
- Recorded Validation0013 as partial-pass evidence only: 140 follow movement events parsed, 5 PASS, 135 REVIEW, 0 FAIL, and no canonical `Data` promotion.
- No command parser, dispatcher, scheduler ticking, sockets, sessions, gameplay, movement execution, party-follow implementation, combat services, spawning services, status-effect ticking, script dispatch, AI execution, world simulation, canonical `Data` rewrite, imported/reference deletion, live parity claim, runtime formula promotion, or timing promotion is included.

## CodeUpdate-0063 - Generic Scheduler Execution Channels

- Recorded CodeUpdate-0062 GitHub Actions as green by user report.
- Corrected scheduler bootstrap channel names to generic execution channels.
- Replaced gameplay-facing channel names with Ingress, Validation, Planning, Dispatch, Mutation, TimedWork, EventWork, Persistence, Observation, and Maintenance.
- Folded ServerRuntimeValidation0009 into the no-.NET simulator validation surface with explicit expected-model combat lifecycle state and death-triggered respawn scheduling.
- Updated smoke coverage to verify the generic channel set remains runtime-disabled.
- Updated ServerSim0036 scheduler mapping to clarify gameplay-facing findings are evidence only and must not hardcode the reusable host scheduler.
- No scheduler ticking, sockets, sessions, gameplay, runtime rule execution, mode/profile activation, combat services, movement execution, spawning services, status-effect ticking, script dispatch, AI execution, character creation, world simulation, networking expansion, live editor behavior, canonical `Data` rewrite, imported/reference deletion, live parity claim, runtime formula promotion, or timing promotion is included.

## CodeUpdate-0062 - Scheduler Bootstrap Diagnostics

- Recorded CodeUpdate-0061 GitHub Actions as green by user report.
- Added read-only Server scheduler bootstrap audit and report models.
- Attached `scheduler-bootstrap` to Server bootstrap.
- Added Server startup output for scheduler bootstrap status and disabled channels.
- Added smoke coverage that verifies scheduler channels remain runtime-disabled.
- Registered disabled diagnostic scheduler channels; CodeUpdate-0063 supersedes the original gameplay-facing names with generic execution names.
- Folded the planning-window ServerSim0036 handoff into curated evidence/planning docs under `Docs/ServerSim/ServerSim0036/`.
- No scheduler ticking, sockets, sessions, gameplay, runtime rule execution, mode/profile activation, combat, movement, spawning, status-effect ticking, script dispatch, AI execution, character creation, world simulation, networking expansion, live editor behavior, canonical `Data` rewrite, imported/reference deletion, runtime formula promotion, or timing promotion is included.

## CodeUpdate-0061 - Host Lifecycle Readiness State

- Recorded CodeUpdate-0060 GitHub Actions as green by user report.
- Added read-only Server host lifecycle audit and report models.
- Attached `host-lifecycle` to Server bootstrap.
- Added Server startup output for lifecycle status and per-stage readiness.
- Added smoke coverage that verifies the lifecycle remains `ready-runtime-disabled`.
- Verified lifecycle stages derive from existing host configuration, data catalog, world projection, module catalog, and module readiness reports.
- No sockets, sessions, gameplay, runtime rule execution, mode/profile activation, combat, character creation, world simulation, networking expansion, live editor behavior, canonical `Data` rewrite, imported/reference deletion, runtime formula promotion, or timing promotion is included.

## CodeUpdate-0060 - Host Configuration Surface

- Recorded CodeUpdate-0059 GitHub Actions as green by user report.
- Added read-only Server host configuration audit and report models.
- Attached `host-configuration` to Server bootstrap.
- Added Server startup output for host configuration status.
- Added smoke coverage that verifies runtime toggles remain disabled.
- Verified defaults: host `Reforged Local Host`, bind `127.0.0.1`, port `4000`, max sessions `64`, world tick `1000ms`, combat tick `6000ms`, gameplay disabled, networking disabled, and world simulation disabled.
- No sockets, sessions, gameplay, runtime rule execution, mode/profile activation, combat, character creation, world simulation, networking expansion, live editor behavior, canonical `Data` rewrite, imported/reference deletion, runtime formula promotion, or timing promotion is included.

## CodeUpdate-0059 - Module Readiness Report

- Recorded CodeUpdate-0058 GitHub Actions as green by user report.
- Added read-only Server module readiness audit and report models.
- Attached `module-readiness` to Server bootstrap.
- Added Server startup output for module readiness status.
- Added smoke coverage that verifies module readiness keeps gameplay disabled.
- Verified `majormud-classic` reports `host-ready-gameplay-disabled`.
- Verified 1 module, 1 host-ready module, 0 gameplay-enabled modules, 0 runtime rule profiles enabled, 12 runtime-ready data categories, 1 runtime-blocked category, 1 placeholder category, and 1,246 world rooms.
- No gameplay, runtime rule execution, mode/profile activation, combat, character creation, world simulation, networking expansion, live editor behavior, canonical `Data` rewrite, imported/reference deletion, runtime formula promotion, or timing promotion is included.

## CodeUpdate-0058 - World Projection Index

- Recorded CodeUpdate-0057 GitHub Actions as green by user report.
- Added scalar string-array collection support to `CoreDataRepositoryLoader`.
- Added read-only Server world projection audit and report models.
- Attached `world-projection` to Server bootstrap.
- Added Server startup output for world projection status.
- Added smoke coverage for world projection indexing and bootstrap attachment.
- Verified `world_rooms` now loads 1,246 records from `Data/world.json`.
- Verified world projection has 1,246 unique room ids, 1,246 canonical rooms, 0 missing references, 0 duplicates, and start room `wcc_map1_room1` present.
- No canonical `Data` rewrite, imported/reference deletion, gameplay, combat, character creation, world simulation, networking expansion, live editor behavior, runtime formula promotion, or timing promotion is included.

## CodeUpdate-0057 - Modular Server Foundation

- Verified local `HEAD`, local `origin/main`, and remote `refs/heads/main` at `4277723ce284be6dbf689711d3ac6e93954dfb05` before edits.
- Recorded that the verified commit was `CoreUpdate 0056` while continuity files still recorded `CodeUpdate-0055`.
- Added a non-gameplay game module catalog to Server bootstrap.
- Registered `majormud-classic` as the first disabled data-driven module baseline using `Data/`.
- Added a disabled `classic` rule profile registration.
- Printed game module and rule-profile status in Server bootstrap output.
- Added smoke coverage for the Server module catalog.
- Preserved Server Build Session 0001 handoff material under `Docs/ServerBuild/Session0001/`.
- Added interactive client map planning placeholder under `Docs/Architecture/InteractiveClientMap.md`.
- Updated continuity documents and manifests to `CodeUpdate-0057`.
- No gameplay system, combat, character creation, world simulation loop, networking expansion, live editor behavior, canonical `Data` rewrite, imported/reference deletion, runtime formula promotion, or timing promotion is included.

## CodeUpdate-0055 - Validation0043, MegaMUD2 RE0021, and Placeholder Audit

- Preserved 19 curated current-pass files from `MajorMUD-Reforged-Living-Validation0043-Cumulative.zip` under `DataDecodeCheckpoints/Validation0043/`.
- Recorded Validation0043 ZIP SHA-256 `03A164E1771CAF9B854205D220C0CFE84DCD34D1CE3FA5A80D586C61657E9BC0`.
- Verified 92 Validation0043 package `Data/` files hash-match current repository `Data/`; no canonical `Data` overlay was applied.
- Preserved 15 curated current-pass files from `ReforgedRealms-MegaMUD2-Living-RE0021-PacketEvidenceExpansion-Real-Cumulative.zip` under `DataDecodeCheckpoints/MegaMUD2RE0021/`.
- Recorded MegaMUD2 RE0021 ZIP SHA-256 `6EEC25347C63D1E6E2ABB58666A65C882D5A6DDA5DA95785A108DDAB6EB4A7BC`.
- Repaired `Tools/DataDecode/Build-UnifiedDecodedData.ps1` so populated rooms, shops, and monsters are not described as placeholders.
- Regenerated `DataDecodeCheckpoints/UnifiedDecodedData/` against the current `Data` baseline.
- Added `Reports/CodeUpdates/CodeUpdate-0055/CurrentDataPlaceholderAudit.csv`.
- Added `Reports/CodeUpdates/CodeUpdate-0055/RuntimeParityBlockerSummary.csv`.
- Added `Reports/CodeUpdates/CodeUpdate-0055/PackageIntakeInventory.csv`.
- Added `Reports/CodeUpdates/CodeUpdate-0055/Validation0043MegaMUD2RE0021PlaceholderAuditReport.md`.
- Recorded `Data/quests.json` as the only true empty canonical placeholder and `world_rooms` as a loader projection gap.
- Recorded the stronger data-driven game-logic policy in continuity documents.
- Verified manifest JSON parse, report CSV parse, `git diff --check`, sequential restore/build for Core, Server, Importer, Editor, Launcher, Client, and Core.Tests, core/server smoke tests, and Server bootstrap run locally.
- No canonical `Data` overlay, field promotion, gameplay system, server behavior change, raw packet inference, imported/reference deletion, or live runtime validation claim is included.

## CodeUpdate-0054 - Validation0013 and DocUpdate0024 New-Files Intake

- Recorded CodeUpdate-0053 GitHub Actions as green by user report.
- Inspected `MajorMUD-Reforged-Validation0013-Cumulative.zip`.
- Recorded Validation0013 ZIP SHA-256 `5E8804557B371C526F48FEF2C7333F344A66316FF48ECE7C3E209FD260297AD1`.
- Copied 437 repository-new files from Validation0013.
- Skipped 86 existing repository paths from Validation0013 without overwriting them.
- Inspected `DocUpdate-0024-Librarium-Calculation-Engine-Placeholder.zip`.
- Recorded DocUpdate0024 ZIP SHA-256 `2022CF91CDF85D5E0136B7765DF9B8EE7D0F193C6E9FCCE4A2B2AF59C8DEED02`.
- Copied 2 repository-new files from DocUpdate0024.
- Added copy/skip inventories under `Reports/CodeUpdates/CodeUpdate-0054/`.
- Added `Reports/CodeUpdates/CodeUpdate-0054/Validation0013DocUpdate0024NewFilesIntakeReport.md`.
- Verified copied JSON files parsed successfully: 72.
- Verified copied CSV files were readable: 183.
- Verified `Tools/MDBExtractor/offline_mdb_text_extractor.py` with Python bytecode compilation.
- No existing repository file overwrite, CodeUpdate-0053 `Data` baseline replacement, server behavior change, gameplay system, Patch0071 application, Patch0074 master verification, or live runtime validation claim is included.

## CodeUpdate-0053 - Patch0068 Data Replacement and Handoff Intake

- Recorded CodeUpdate-0052 GitHub Actions as green by user report.
- Inspected and preserved `MajorMUD-Reforged-CodexHandoff-0074-Recreated.zip` under `DataDecodeCheckpoints/Handoff0074/`.
- Recorded Handoff0074 ZIP SHA-256 `B6132632A154A43AD32C1AA8CEE681E82D994CCA82A360C7728BBF64228D8AD8`.
- Verified 5 of 6 Handoff0074 checksum rows and documented the supplied `SHA256SUMS.txt` self-hash mismatch.
- Inspected and preserved Patch0068 findings under `DataDecodeCheckpoints/Patch0068/`.
- Recorded Patch0068 ZIP SHA-256 `16A8C3932CF23B2B9AB2D3E9DFCA605ABA4EA09FE8BD2E77519AA385CDFD515C`.
- Verified 7 of 7 copied Patch0068 manifest-listed files against `PATCH_0068_MANIFEST.csv`.
- Replaced only repository `Data/` from the embedded Patch0068 `/Data` payload.
- Recorded that exactly 86 embedded Patch0068 `Data/` files were staged and copied.
- Intentionally did not keep duplicate `Docs/PatchHandoffs/` copies; preserved findings live once under `DataDecodeCheckpoints/`.
- Recorded Patch0071 ZIP SHA-256 `56B2A080BA89263F08BE134DA8DE547D941AF3B8D0CE5E4D5B9BCF1C5340F881`.
- Recorded Patch0071 as unreadable by the built-in ZIP reader with `End of Central Directory record could not be found`; Patch0071 was not applied.
- Added `Reports/CodeUpdates/CodeUpdate-0053/Handoff0074Patch0071Patch0068IntakeReport.md`.
- Updated server data-readiness diagnostics and smoke expectations so populated Patch0068 data categories are no longer treated as empty placeholders.
- No non-`Data` Patch0068 overlay, Patch0071 application, Patch0074 master verification implementation, gameplay server behavior change, gameplay system, imported/reference deletion, or live runtime validation claim is included.

## CodeUpdate-0052 - Build0027 Current-State Handoff

- Recorded CodeUpdate-0051 GitHub Actions as green by user report.
- Inspected `MajorMUD-Reforged-CodexHandoff-CurrentState-Build0027.zip`.
- Recorded Build0027 ZIP SHA-256 `7932FA3E6D4664D5440C0039CBF7FD85528CB0A5CBC09C7BE3C25CB2E4CF750F`.
- Preserved the full Build0027 handoff under `DataDecodeCheckpoints/CurrentStateBuild0027/`.
- Preserved human-readable Build0027 docs, tasks, and evidence manifest under `Docs/BehavioralRecovery/Build0027/`.
- Added `Reports/CodeUpdates/CodeUpdate-0052/Build0027CurrentStateHandoffReport.md`.
- Recorded that static data/content recovery and entity/world/catalog recovery are effectively complete by handoff statement, while original runtime validation remains unavailable.
- Recorded that Build0024/0025 matrices remain `expected_model` until converted into actual executable simulator tests.
- Recorded that prior chat pass/fail claims are not authoritative unless backed by artifact logs or locally generated test output.
- No executable harness implementation, canonical `Data` rewrite, server behavior change, field promotion, gameplay system, or original runtime behavior certification is included.

## CodeUpdate-0051 - Import Staging and Build0021 Behavioral Recovery

- Recorded CodeUpdate-0050 GitHub Actions as green by user report.
- Added `Tools/DataDecode/Build-ImportStagingReport.ps1`.
- Generated `DataDecodeCheckpoints/ImportStaging/` with canonical data surface, simulator evidence surface, import staging queue, phone handoff template, manifest, README, and report files.
- Preserved the user-provided Build0021 behavioral recovery evidence package under `Docs/BehavioralRecovery/Build0021/` and `DataDecodeCheckpoints/SimulatorBuild0021/`.
- Added `Docs/CodeUpdates/CODEX_BUILD0021_REPO_UPDATE.md`.
- Added `Core/Runtime/BehavioralRecovery/README.md`.
- Added `Reports/CodeUpdates/CodeUpdate-0051/ImportStagingAndBuild0021Report.md`.
- Recorded import staging counts: 1,246 evidence-backed room records, 178 shops, 732 monster item drop edges, 125 quest/progression candidates, 46 skill/trainer-related rows, 1,770 item/equipment records, 1,180 spell records, and 1,100 expected monster records.
- Recorded Build0021 formula/catalog confidence counts: 6 `verified_from_data`, 4 `strong_candidate`, 2 `weak_candidate`, and 4 `runtime_required`.
- No canonical `Data` rewrites, server behavior changes, field promotions, gameplay systems, live runtime validation claims, or Build0021 formula certifications are included.

## CodeUpdate-0050 - SimulatorBuild0002 Handoff and DataDecode0110 Static Certification

- Preserved the user-provided SimulatorBuild0002 Codex handoff under `DataDecodeCheckpoints/SimulatorBuild0002/`.
- Extracted final DataDecode0110 static certification files under `DataDecodeCheckpoints/DataDecode0110/`.
- Added selected DataDecode0099-0108 DAT decoding decision docs under `Docs/DATDecoding/`.
- Installed the expected-model simulator under `Tools/Simulator/MajorMUD-Reforged-SimulatorPrototype/`.
- Recorded source package SHA-256 `66DF6BF0E1885A96C1B3BFB9A9D535FEDA8FA9D025A807509175B4C644D758DA`.
- Recorded DataDecode0110 metrics: 3,908 imported database files checked, 1 MDB present, 5 CSV files present, 0 unsupported inferences, 3,975 unresolved ownership rows carried forward, and 5,464 unresolved static rows carried forward.
- Recorded that SimulatorBuild0002 is expected-model evidence only and does not certify live Worldgroup behavior.
- Excluded generated Python `__pycache__` files and the embedded stale full repository archive.
- No canonical `Data` rewrites, server behavior changes, field promotions, gameplay systems, or live runtime validation are included.

## CodeUpdate-0049 - DataDecode0068 Spell Engine Proof

- Preserved the user-provided DataDecode0068 Spell Engine Proof checkpoint under `DataDecodeCheckpoints/DataDecode0068/`.
- Recorded DataDecode0068 package SHA-256 `587B92CE0495BF20C0619E4F2139CAA980F17A2EAE70A8C36A7A4162374CAFE6`.
- Recorded spell proof metrics: 1,180 canonical spells, 930 native WCCSPELS records, 1,789 native WCCSPELS string rows, 1,350 OmniVerse spell reference records, 1,151 canonical-to-OmniVerse spell matches, 41 raw field positions, 439 WGSERVER spell constant probes, and 0 field promotions.
- Refreshed `DataDecodeCheckpoints/UnifiedDecodedData/`; the source inventory now records 1,701 rows.
- Recorded that DataDecode0051 through DataDecode0067 are not claimed as repo-preserved unless supplied later.
- No canonical `Data` rewrites, server behavior changes, spell field promotions, gameplay systems, executable execution, or OmniVerse-only promotions are included.

## CodeUpdate-0048 - Unified Decoded Data and Phone Workflow

- Added `Tools/DataDecode/Build-UnifiedDecodedData.ps1` to generate a repo-native decoded-data inventory and OmniVerse comparison surface.
- Added `DataDecodeCheckpoints/UnifiedDecodedData/` with generated manifest, source inventory, category summary, and comparison CSV reports.
- Added Android ChatGPT decode workflow docs under `Docs/DATDecoding/AndroidChatGPTDecodeWorkflow.md`.
- Added phone handoff templates under `DataDecodeCheckpoints/PhoneHandoffTemplates/`.
- Recorded comparison findings: classes/races have full normalized-name coverage, spells have partial `sourceId` coverage, items are name-only correlation, and shops/rooms/monsters remain incomplete as Reforged-native canonical data.
- No canonical `Data` rewrites, server behavior changes, field promotions, gameplay systems, or imported/reference deletions are included.

## CodeUpdate-0047 - DataDecode GitHub Transition

- Preserved the user-provided DataDecode0049 WGSERVER record access / structure correlation checkpoint under `DataDecodeCheckpoints/DataDecode0049/`.
- Recorded DataDecode0049 package SHA-256 `8869C12860A5D645F5EBA5B8F6F51C9389A98E96A3ABA7B8CBB57288CCCE8A2B`.
- Recorded `wgserver.exe` reference hash `4DA28D718CF99701AA63C1889770F293BB3EC828AEA5E8BEA500473D2698C73F` without copying or executing the binary.
- Added DataDecode0050 GitHub transition handoff and metrics under `DataDecodeCheckpoints/DataDecode0050/`.
- Updated continuity documents so future decode passes use the GitHub repository as the direct working copy; ZIPs are archival/checkpoint artifacts only.
- No canonical `Data` rewrites, server behavior changes, field promotions, gameplay systems, executable execution, or public-ID to binary-row assumptions are included.

## CodeUpdate-0046 - OmniVerse Capture Checkpoint

- Recorded CodeUpdate-0045 as green by user report.
- Recorded the HTTrack OmniVerse SPA shell/assets capture under `E:\Httracks\OmniVerseCodexFull`.
- Imported the gathered OmniVerse/MudInfo reference payload under `ImportedDatabases/OmniVerseReference/CodeUpdate0046/` for validation/correlation only.
- Recorded direct WCC OmniVerse API capture files for abilities, classes, items, monsters, races, shops, spells, world status, 168 shop-seeded roommaps, 1,246 room details, and a 23-entry room capture error log.
- Recorded and preserved MudInfo `maps.rar` archive hash, source archive, and 102 extracted MudInfo map files.
- Documented that HTTrack alone does not capture OmniVerse codex records because the site loads data through API calls.
- Documented that the committed reference payload excludes exploratory HTTrack cache/test files to keep final 0046 evidence clean.
- No canonical `Data` rewrites, server behavior changes, field promotions, gameplay systems, or public-ID to binary-row assumptions are included.

## CodeUpdate-0045 - DataDecode0043 Checkpoint Integration

- Added the user-provided `DataDecode0043-ImportReadinessValidation-PreMirror` checkpoint under `DataDecodeCheckpoints/DataDecode0043/`.
- Recorded DataDecode0043 as the pre-mirror import-readiness baseline: 96-97% static decode estimate, 6 canonical schemas checked, 16 repository JSON datasets checked, 1,000 foreign-key candidates reviewed, 83 native source files hashed, 5 import blockers registered, and 0 canonical field promotions.
- Documented that original MajorMUD binary files remain authoritative and OmniVerse/public IDs must not be assumed to equal binary record indexes.
- Documented package verification limitations: `DataDecode0043_Manifest.csv` and checkpoint `UpdateManifest.json` do not match their own recorded manifest size/hash entries.
- No server behavior, gameplay, networking, editor implementation, canonical `Data` rewrite, imported data deletion, field promotion, or production import is included.

## CodeUpdate-0044 - Original Reference Extractor

- Added `Tools/ReferenceCapture/Capture-OriginalReferenceData.ps1` for reference-only capture of original Worldgroup, WCC, WBB, WSGSERVE, and related source material.
- Added source preservation, ZIP extraction, optional 7-Zip-backed RAR/7z extraction, SHA-256 file inventory, category hints, printable string extraction, and JSON/CSV/Markdown reports.
- Added `Tools/ReferenceCapture/README.md` with safe usage and extraction boundaries.
- Recorded CodeUpdate-0043 GitHub Actions as green by user report.
- Preserved the rule that imported/reference outputs are evidence only and cannot overwrite canonical `Data/` without a reviewed follow-up update.
- No gameplay systems, combat, character creation, world simulation, networking expansion, editor implementation, canonical `Data` rewrite, imported data deletion, original executable execution, keygen use, license bypass, or exact parity claim is included.

## CodeUpdate-0043 - Server Data Readiness Classification

- Added runtime-readiness classification to canonical Server data catalog summaries.
- Separated file presence, record loading, placeholder status, runtime-ready status, and readiness notes.
- Updated Server startup output to report runtime-ready and runtime-blocked category counts.
- Added smoke-test coverage for placeholder categories and runtime-readiness classification.
- Recorded the long-term goal to retire `ImportedDatabases/` only after Reforged-native `Data/` is complete, verified, and explicitly approved for cleanup.
- No gameplay systems, combat, character creation, world simulation, networking expansion, live editor implementation, data rewrite, WCC import, imported data deletion, proprietary binary parser, or exact parity claim is included.

## CodeUpdate-0042 - Server Data Catalog Bootstrap

- Added read-only Server data catalog loading through the existing Core `CoreDataRepositoryLoader`.
- Attached canonical `Data/` category readiness to non-gameplay Server bootstrap.
- Updated Server startup output to print canonical data category status, file sizes, and loaded record counts.
- Added smoke-test coverage for Server data catalog attachment and direct catalog audit loading.
- Local bootstrap loads 13/13 registered categories and 4,527 records, with 5 empty placeholder categories.
- No gameplay systems, combat, character creation, world simulation, networking expansion, live editor implementation, data rewrite, WCC import, proprietary binary parser, or exact parity claim is included.

## CodeUpdate-0041 - Classic Parity Audit Foundation

- Added read-only Classic parity audit inventory for canonical `Data/` and imported reference material.
- Added broad category summaries for rooms, monsters, items, spells, shops, quests, classes/races, ganghouses, maps/paths, and raw legacy databases.
- Attached the audit to non-gameplay Server bootstrap and startup output.
- Added smoke-test coverage for audit inventory and bootstrap attachment.
- Recorded that all WCC data is reference-only unless the user explicitly approves a reviewed import/reconciliation update.
- No gameplay systems, combat, character creation, world simulation, networking expansion, live editor implementation, data rewrite, imported-reference overwrite, proprietary binary parser, or exact parity claim is included.

## CodeUpdate-0040 - WCC-Labeled WBB Imported Reference Archive

- Preserved the user-provided `wbbmud.zip` archive under `ImportedDatabases/WCCLabeledWBB/` as reference/import material.
- Added archive manifest, file inventory, source archive, extracted reference files, and import report.
- Recorded that the archive is WCC-labeled WBB because the source BBS hosted two different games.
- No canonical `Data/` rewrite, gameplay implementation, runtime dependency on legacy executables/DLLs, or overwrite of existing imported archives is included.

## CodeUpdate-0039 - Server Bootstrap Readiness Summary

- Added a compact host-tool readiness summary to the non-gameplay Server bootstrap report.
- Added readiness flags for project root, required directories, manifest identity, startup diagnostics, and host-tool readiness.
- Updated Server startup output to print readiness status while keeping gameplay status disabled.
- Added smoke-test coverage for readiness status.
- Folded in the Classic-first Server direction as planning/continuity documentation.
- Folded in engine-first architecture direction as planning/continuity documentation.
- Added the user-provided WCC default install under `ImportedDatabases/WCCDefaultInstall/` as reference/import material.
- Recorded CodeUpdate-0038 GitHub Actions as green by user report.
- No gameplay systems, combat, character creation, world simulation, networking expansion, live editor implementation, Reforged mode, Custom mode, AI systems, alternate gameplay rules, canonical `Data` rewrite, or `ImportedDatabases` overwrite is included.
- Local .NET 8.0.422 JSON parse, affected Release builds, smoke tests, Server bootstrap run, and WCC default archive integrity verification passed after folded documentation/import work.

## CodeUpdate-0038 - Server Bootstrap Directory Status

- Added required-directory status records to the non-gameplay Server bootstrap report.
- Added required-directory total, present, and missing counts for future host/editor tooling.
- Updated Server startup output to print required-directory summary and detail lines.
- Added smoke-test coverage for required-directory status.
- Recorded CodeUpdate-0037 GitHub Actions as green by user report.
- No gameplay systems, combat, character creation, world simulation, networking expansion, live editor implementation, data rewrite, or `ImportedDatabases` overwrite is included.
- Local .NET 8.0.422 JSON parse, restore, sequential Release build, smoke tests, and Server bootstrap run passed.
- GitHub Actions is green by user report.

## CodeUpdate-0037 - Software Design Specification and Continuity Cleanup

- Combined the user-provided CodeUpdate-0036B continuity cleanup package and CodeUpdate-0037 Software Design Specification package into one local documentation update.
- Fetched GitHub truth and fast-forwarded local `main` to CodeUpdate-0036 before applying the combined update.
- Repaired stale continuity documents that still referenced CodeUpdate-0035 after CodeUpdate-0036 direct-GitHub commits.
- Added draft `Docs/SDS/` Software Design Specification planning records for future AI, combat, quest, explorer, discovery map, launcher, host editor, loot, networking, plugin, expansion, data-format, gameplay-flag, and ganghouse systems.
- Added CodeUpdate-0037 documentation and verification report.
- No gameplay systems, combat, character creation, world simulation, networking expansion, live editor implementation, source-code behavior changes, data rewrite, or third-party raw archive import is included.
- JSON parse and local .NET 8.0.422 restore/build/smoke verification passed.
- GitHub Actions is green by user report.

## CodeUpdate-0036 - Knowledge Base and Design Decisions

- Added a CodeUpdate-0036 documentation plan for the Reforged knowledge base, design decisions, and reference-source policy.
- Added `KnowledgeBase/DesignDecisions/DesignDecisionIndex.md` to track accepted/proposed design decisions.
- Added `KnowledgeBase/Research/ReferenceSourceCatalog.md` to catalog WCC, WSGSERVE, LLT XML, MegaMUD, Mud Hex, OmniVerse, MudInfo, MudCentral, Reddit/community sources, and other verified references.
- Added `KnowledgeBase/MeetingNotes/CodeUpdate-0036-Discussion-Recap.md` to preserve the major design discussion after CodeUpdate-0035.
- Recorded the user-reported green GitHub Actions result for CodeUpdate-0035.
- No gameplay systems, combat, character creation, world simulation, networking expansion, live editor implementation, AI runtime, data rewrite, or raw third-party binary/reference archive import was added.
- CodeUpdate-0036 is documentation/research only; no local build was run in ChatGPT.
- GitHub Actions remains pending until the user verifies this direct GitHub documentation update.

## CodeUpdate-0035 - Server Bootstrap Validation Summary

- Added validation issue-count properties to the non-gameplay Server bootstrap report.
- Server bootstrap console output now includes a validation summary by severity.
- Smoke tests verify success and failure bootstrap paths expose expected validation issue counts.
- Recorded the user-reported green GitHub Actions result for CodeUpdate-0034.
- No gameplay systems, combat, character creation, world simulation, networking expansion, live editor implementation, or data rewrite was added.
- Local .NET 8.0.422 restore/build/smoke verification passed.
- GitHub Actions is green by user report.

## CodeUpdate-0034 - OmniVerse Reference Audit Plan

- Recorded OmniVerse as a read-only reference source for future Reforged data audits.
- Documented verified public reference endpoints for MajorMUD world/status, codex item/spell samples, and shop/trainer data.
- Added the rule that OmniVerse may suggest corrections but must not automatically overwrite canonical Reforged `Data/`.
- Recorded the user-reported green GitHub Actions result for CodeUpdate-0033.
- Updated continuity documents, manifests, release notes, roadmap, and audit report.
- No gameplay systems, combat, character creation, world simulation, networking expansion, live editor implementation, or data rewrite was added.
- Local .NET 8.0.422 restore/build/smoke verification passed.
- No files under `Data/` or `ImportedDatabases/` were changed.
- GitHub Actions is green by user report.

## CodeUpdate-0033 - Server Bootstrap Exit Code Contract

- Added an explicit `ExitCode` contract to the non-gameplay Server bootstrap report.
- Server startup now returns the bootstrap report exit code instead of duplicating success/failure code values.
- Smoke tests now verify bootstrap success exposes exit code 0 and bootstrap failure exposes exit code 1.
- Recorded the user-reported green GitHub Actions result for CodeUpdate-0032.
- No gameplay systems, combat, character creation, world simulation, networking expansion, or live editor implementation were added.
- Local .NET 8.0.422 restore/build/smoke verification passed.
- GitHub Actions is green by user report.

## CodeUpdate-0032 - Server Bootstrap Manifest Identity

- Added ProjectManifest identity capture to the non-gameplay Server bootstrap report.
- Server bootstrap output now includes project, CodeUpdate, database version, and milestone values.
- Added smoke-test coverage to verify the bootstrap captures manifest identity fields.
- Updated Server documentation and continuity records.
- Added a handoff improvement: future updates should record a short next-scope candidate and explicit boundaries separately from completed work.
- No gameplay systems, combat, character creation, world simulation, networking expansion, or live editor implementation were added.
- Local .NET 8.0.422 restore/build/smoke verification passed.
- GitHub Actions is green by user report.

## CodeUpdate-0031A - Server Smoke Test Compile Fix

- Corrected invalid C# string escaping in `Tests/Source/Reforged.Core.Tests/Program.cs`.
- Fixed the GitHub Actions test build failure reported at `Program.cs#L59` with `Syntax error, ',' expected`.
- Recorded CodeUpdate-0031A as a corrective build-fix patch for CodeUpdate-0031.
- Updated continuity documents, release notes, ProjectManifest, roadmap, and this changelog.
- No gameplay systems, combat, character creation, world simulation, networking expansion, or live editor implementation were added.
- User reported CodeUpdate-0031A GitHub Actions green.
- Codex local .NET 8.0.422 sequential restore/build/smoke verification passed.

## CodeUpdate-0031 - Server Bootstrap Foundation

- Added `Apps/Source/Reforged.Server/Program.cs` as the first non-gameplay Server entry point.
- Added `ServerBootstrap` and `ServerBootstrapReport` to verify repository root, required folders, ProjectManifest identity, and existing Server startup diagnostics.
- Updated `Reforged.Server.csproj` to build as an executable project.
- Added a Server bootstrap smoke test to `Tests/Source/Reforged.Core.Tests/Program.cs`.
- Added `DEVELOPMENT_PRINCIPLES.md` to separate engineering principles from agent workflow rules.
- Updated continuity documents, roadmap, release notes, ProjectManifest, and Server source README.
- No gameplay systems, combat, character creation, world simulation, networking expansion, or live editor implementation were added.
- GitHub Actions failed after push due to a syntax error in the smoke-test JSON string; CodeUpdate-0031A corrects this.

## CodeUpdate-0030 - Truth Documentation Cleanup and Agent Handoff Rules

- Added root `AGENTS.md` with mandatory handoff rules for ChatGPT, Codex, and future developers.
- Recorded that GitHub repository `ShisenShi1017/MajorMud-Reforged-V1.0` is the authoritative source of truth.
- Added the required mode check: Codex mode updates local only and waits for user push plus stated green GitHub Actions before continuing; ChatGPT mode uses verified patch ZIPs for local application.
- Added the permanent rule: never guess when creating, fixing, reviewing, packaging, or reporting an update.
- Updated continuity documents to remove or replace stale source-of-truth and Milestone 1/old CodeUpdate direction.
- Updated Server documentation to reflect CodeUpdate-0029 startup-only diagnostics.
- Added a CodeUpdate-0030 report.
- No source implementation code or gameplay features were added.
- The user reported GitHub Actions green for CodeUpdate-0030.

## CodeUpdate-0029 - Host Server Startup Diagnostics

- Added startup-only Server diagnostics for the first Milestone 2 implementation step.
- Wired Server startup checks through existing Core configuration, logging, service registry, event bus, repository health, and startup diagnostics.
- Added a Server startup diagnostics smoke test.
- Updated the Server README and continuity documents.
- Local .NET 8.0.422 restore/build/core-server-smoke verification passed.
- No gameplay systems, combat, character creation, world simulation, editor implementation, or networking expansion were added.

## CodeUpdate-0028 - Milestone 2 Scope Plan

- Recorded the user-reported green CodeUpdate-0027 GitHub Actions result.
- Added the initial Milestone 2 scope plan.
- Recommended CodeUpdate-0029 as host/server startup and runtime diagnostics foundation work, pending user approval.
- Updated continuity documents, manifests, and roadmap notes for Milestone 2 planning.
- Local .NET 8.0.422 restore/build/smoke-test verification passed.
- No gameplay feature work or implementation code was introduced.
- No BAT installer, update ZIP, or GitHub push was created by Codex.

## Earlier History

Earlier CodeUpdate history is preserved in repository reports and handoff documents. Current sessions should continue from the latest verified Code Update recorded at the top of this file.
