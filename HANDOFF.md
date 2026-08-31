# Handoff

Current baseline:

- Code Update: `0140`
- Database Version: `0010`
- Milestone: CodeUpdate0140 - Classic Parity Closure and Creator Studio
- Status: locally built and smoke-verified repository update; GitHub Actions pending push.
- Parity: 169 PASS / 31 STRONG / 0 UNKNOWN (84.5% strict / 100.0% evidence-backed).
- CodeUpdate0365 Quests / Scripts / Triggers: 2/2 PASS; source/static/data-correlation verification 65/65 PASS; runtime projection requires 83 executable quests, 83 live quest triggers, 182 linked stage scripts, and 2,616 decoded operations; remoteaction is 139/139 invocations / 85/85 executable signatures.
- CodeUpdate0363 Items & Inventory: 6/6 PASS; direct native/source/data verification 66/66 PASS.
- CodeUpdate0362 Import / Export / Limits / Recovery: 4/4 PASS; source/data/native verification 86/86 PASS.
- Runtime: native exit, formula, NPC combat, scheduler/regeneration, character death, death-floor, session lifecycle, party coordination, gang communications/invitations, daily gang-house cleanup, organization services, communication rendering, character-only BREAK, GIVE, PvP LVLDIFF, Economy & Services, and the Host/Persistence/Worldgroup boundary are evidence-verified.
- Evidence: parity passes 0345-0362 are folded into the cumulative ledger; the generic earlier native/direct-static verification report retains 2,681 supplied checks with zero failures, and supplemental 0359/0360/0361/0362/0363 verification is 34/34, 41/41, 92/92, 86/86, and 66/66.
- Compatibility: MegaMUD/Borin CP437 Telnet, client-data negotiation, route compatibility, REROLL/SET KEEP, and SET WARNING are locally verified.
- Limitation: two pre-existing corpse-resurrection equipment item-smoke assertions remain open.
- Current-health validator: 44/45 executed stages pass; execution stops truthfully at those two `server_items` assertions.
- Local integration gates: Reforged.Server Release builds with zero warnings/errors; focused Host/Persistence, Import/Export/Limits/Recovery, Items/Inventory, NPC/AI, Quests/Scripts/Triggers, and cumulative parity gates pass.
- Application startup: heavy Play/Borin/Editor/Librarium work is deferred until use; the published app opens a responsive `MajorMUD Reforged` window locally.
- Creator Studio bridge: supported Nightmare Redux MDB tables can be analyzed/imported into Custom1 and exported to a new Jet MDB. Original MDB and WCC/WGS inputs are read-only; unmapped columns and evidence anchors are retained rather than guessed into gameplay. Real `ability.mdb` read and temporary write/read round-trip checks pass locally. No Custom1 import was performed during this intake.

Current authority and validation:

- Librarium is the gameplay authority; generated Data is untracked runtime output.
- Cumulative ledger: Librarium/Parity/majormud_parity_assertions.json.
- Current validator: python Tools/validator.py.
- Runtime parity gate: --smoke-classic-parity-closure.
- Accepted one-time decode and parity validation is recorded in ValidationManifest.json; historical pass snapshots and validators are retired.
- Distinct runtime authority classes remain separate because they own different behavior and evidence boundaries.

Latest exact closure:

- The original WCCMMUD 1.11p DLL plus wccknms2.dat proves Nahr (`npc.1006`) has equipped-item ID zero at the native record field.
- Native execution NULs both the attack-verb and weapon-name buffers before all five message-8480 calls.
- Preserve both empty strings and the resulting awkward native spacing; do not normalize the output.

Protected local state:

- Do not commit generated Data, live Borin runtime/training state, saves, imported-data archives, or release output unless the user explicitly requests it.

## Guardian Hall Phase 3-0150

Realm Keeper local telemetry is active through `RealmKeeperTelemetryService`. Continue Phase 3 with authenticated host telemetry and the Wanderer runtime-session connector. Do not replace this evidence-based local connector with simulated values.

## Guardian Hall Phase 3-0151 Handoff
Wanderer local character/session telemetry is implemented. Continue next with Analyst runtime datasets and report generation while preserving the role-specific workspaces.

## Guardian Hall Phase 3-0152

Analyst runtime data wiring is complete. `AnalystTelemetryService` performs target-specific read-only scans over generated `/Data`, updates observable source and report counts, and feeds graph/trend summaries and exports. No character selector or runtime mutation was introduced. Continue next with Worldsmith test-realm proposal/simulation integration or Archivist decoder/evidence pipeline integration.

## Guardian Hall Phase 3-0153
Worldsmith runtime integration is complete through safe baseline inspection, proposal persistence, and non-mutating simulation. Next Guardian runtime slice is Archivist decode/evidence pipeline integration.

## Guardian Hall Phase 3-0154

Archivist runtime evidence decoding is implemented. The next Guardian Hall pass should connect Archivist search and relationship selection to indexed evidence browsing and then add explicit reviewed Librarium promotion without silent writes.

## Guardian Hall Phase 3-0155

Archivist report/export completion and editable local-tool execution are implemented. Reports include findings, evidence, confidence, relationships, suggested-runtime cautions, and open questions. Codex exports are complete multi-file ZIP packages. Local tools persist executable path, arguments, working directory, enabled state, validation status, launch time, and errors; validation blocks invalid paths before launch. Librarium remains preview-only.

## Guardian Hall Phase 3-0156

Worldsmith approval-controlled staging is complete. Generated proposals expose a confirmation code; users must re-enter it to create a hash-bound approval artifact. Approved proposals can then be staged only under the Guardian Hall Worldsmith isolated realm/project workspace with rollback backup and apply receipt. Live Realm and direct runtime/Librarium mutation remain blocked. Continue next with authenticated host/session connectors, persistence migration, or final visual polish.

## Guardian Hall Phase 3-0159
Remote Realm Keeper telemetry endpoints are implemented with editable URL/path/scope/timeout configuration, environment-variable bearer authentication, status/error persistence, and verified local-plus-remote aggregation.

## Guardian Hall Phase 3-0161
Analyst historical trend persistence, visual family series, comparison deltas, and configurable multi-format exports are implemented and validated.

## Guardian Hall Phase 3-0162
Archivist indexed evidence search and relationship browsing are implemented. Continue with folder/drag-drop intake hardening, reviewed Librarium promotion design, or final fantasy command-center visual polish while retaining preview-only safety until explicit promotion approval exists.

## Guardian Hall Phase 3-0163
Guardian Hall now presents five visually distinct role chambers without changing role boundaries or merging Forge.


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
## CodeUpdate0364 NPC & AI closure

- CodeUpdate0365 Quests / Scripts / Triggers closure: 2/2 PASS with an exact 83-flow / 83-trigger / 182-script / 2,616-operation executable graph; 2,100 analysis-only quest candidates remain Librarium-only; remoteaction is exhaustive at 139 invocations / 85 executable signatures; source/static/data-correlation verifier is 65/65 PASS.
- CodeUpdate0364 NPC & AI closure: 13/13 PASS; direct native/source/static verification 32/32 PASS; overall parity 167 PASS / 33 STRONG / 0 UNKNOWN (83.5% strict). MonsGuards is ordered same-room spell-target substitution; native fast/medium/slow generation ownership is connected; generic threat/perception/flee/patrol remain non-Classic Reforged extensions.
- Evidence: `Librarium/Parity/majormud_npc_ai_complete_evidence_0364.json`.
- Verification: `Reports/RuntimeVerification/classic_npc_ai_verification.json`.
- Target runtime gate: `--smoke-classic-npc-ai`; full gate: `--smoke-classic-parity-closure`.
