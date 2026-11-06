# RealmGate Project Status

## Current position

**CodeUpdate0142 - RealmGate Client and Character Integration**

Database version: `0010`

CodeUpdate0142 is CLOSED. The RealmGate app Release build and full solution build pass with 0 warnings / 0 errors, the current-health validator passes 29/29, the accepted WPF client smoke baseline is 14/14, the cross-platform client-core suite passes 62/62, and the pushed GitHub Classic parity closure passes.

The repository has reached a clean automated verification baseline after the Classic parity closure, RealmGate client expansion, Creator Studio MDB/WCC provenance work, and repository cleanup.

Post-closure runtime hardening now keeps world/session failures contained: a generated world that fails to load is quarantined while healthy worlds remain available, a failing Telnet session reports the error without stopping the Host, and Host restart/stop cleans up faulted runtime tasks instead of allowing an exception to close the WPF app. The Host workspace now contains its live Sessions and Logs tabs.

Character deletion is available to account owners inside each selected world's character menu and to editors in Creator Studio's world-aware Characters tab. Both paths enforce account/world ownership and refuse deletion of logged-in characters. The character smoke now verifies create, restore, decoded LIGHT equip, permanent delete, and account-slot cleanup.

Classic contextual-command recovery now routes exact room-local gate, object, and decoded script phrases before the ordinary unknown-text-to-SAY fallback. NPC-specific ASK routes remain dialogue rather than room actions, and source-authorized no-keyword roots are available through direct GREET routing. `ROOMCOMMANDS` exposes executable room-local commands and contextual NPC interactions while obeying hidden-exit visibility. The WPF room-command panel sends its selected command through the ordinary server path.

The focused regression passes 7,679 checks across every recovered runtime route: 249 text-command exits with 675 aliases, 831 materialized object aliases, all 1,739 decoded room-action aliases across 493 action rooms, 1,225 non-sentinel NPC dialogue keyword routes, and 37 direct GREET ownership routes across 19 source-authorized no-keyword roots. It verifies transactional PRICE debit and native CAST execution through the live spell engine. Ferryman travel from Newhaven through the pier to Silvermere and back is verified, including disposable character autosave/reconnect. The source audit covers 26,668 rooms, 61,976 exits, 771 NPC-dialogue links, 2,279 textblocks, and 284 committed room interactions with zero actionable discrepancies.

Avalonia Phase 2 now supports local and remote generated-world navigation graphs plus inventory/equipment automation. Remote RealmGate hosts deliver only the selected world's graph through bounded compressed private frames with SHA-256 verification; derived worlds never receive a Classic fallback. The Navigation tab persists world-qualified Favorite and Stash destinations, while the Inventory tab consumes a world/character-qualified server snapshot, persists isolated rules and equipment sets, previews every plan, and routes approved actions through Emergency Stop and the ordinary host command path.

## Verified automated baseline

Latest observed results:

| Gate | Result |
| --- | --- |
| RealmGate app Release build | PASS — 0 warnings, 0 errors |
| Full solution Release build | PASS — 0 warnings, 0 errors |
| Current-health validator | PASS — 29/29, including complete Classic room/dialogue recovery |
| RealmGate client smoke suite | PASS — 14/14 |
| Cross-platform client-core regression | PASS — 81/81 (latest local settings and secure-store slice) |
| Classic parity ledger | 200 PASS / 0 STRONG / 0 UNKNOWN |
| Decode exhaustion in the passing validator run | 49/49 families connected; 2,332/2,332 scalar fields connected; 0 unresolved |

The client smoke suite covers shell/layout, cleanup scheduling, embedded host, automation, map/Librarium, Batch 3 client behavior, inventory automation, recovery automation, combat automation, party automation, diagnostics, parity completion, and help.

The latest full local validator passed 29/29 on 2026-10-25. Its full solution build passed with 0 warnings / 0 errors and its exhaustive room/dialogue gate passed 7,679/7,679 checks. Direct Avalonia Windows and Linux framework-dependent publishes preceded this room/dialogue slice; local standalone Avalonia builds report the known SDK 8/Avalonia 12 analyzer-version warnings and, when the restricted feed is unavailable, `NU1900` vulnerability-index warnings. The Linux launcher is an ELF executable; native Linux GUI interaction has not been tested.

The smoke fixes immediately before 0141 corrected test/cleanup assumptions rather than introducing alternate gameplay behavior:

- invalid-regex smoke now verifies the rejected regex trigger itself instead of requiring every other automation rule to remain silent;
- wildcard capture smoke isolates an unrelated PvP rule that could legitimately win the same-priority dispatch;
- Batch 3 treats the retired local-only MegaMUD `.mp` archive as optional and still validates it if a developer restores that archive;
- client smoke diagnostics can write exact failures to generated local report files.

## Runtime/data state

Classic gameplay data is authoritative through `Librarium/` and generated runtime `Data/`.

Decoded light-source items compile to the existing `LIGHT` equipment channel in both Classic and derived worlds. Runtime presentation reports remaining readiness, such as `(Readied 100/100)`, rather than presenting LIGHT as a body-worn slot.

The accepted Classic parity ledger is:

**200 PASS / 0 STRONG / 0 UNKNOWN = 200**

RealmGate remains independent of a live Worldgroup installation for normal runtime. Original native/WCC/WGS/WBB/imported material is evidence or import input, not a runtime dependency unless a specific workflow explicitly consumes it.

Generated worlds are runtime-standalone. The server now validates each generated `world_manifest.json` and refuses worlds that are incomplete, non-standalone, or permit live Classic fallback. A reversible local test also verified that Custom1 boots and runs its living-world smoke while `Data/Classic` is unavailable.

Custom1 regeneration currently produces 1,103 NPC definitions with 962/962 spawn records resolved and 1,015 live NPC instances. The MDB projection no longer fabricates `behavior.group_id = 0` when the source contains no group column; inherited Classic group identities remain intact.

Borin's current parity preflight still consumes:

`Reports/RuntimeVerification/majormud_parity_scorecard.json`

Do not delete that file as part of report cleanup without first changing Borin's trainer.

## Client state

The complete transitional WPF client capability expansion retains its accepted 14/14 smoke baseline. Shared terminal, transport, protocol, automation, event, navigation, inventory, assistant planning, Character snapshot/item-search behavior, bounded external-session capture, privacy-safe session diagnostics, merge-preserving application defaults, and platform credential selection now have an 81/81 cross-platform client-core regression suite.

The Avalonia Play surface now includes live Telnet/ANSI operation, automation, timed events, verified Goto/Auto-Roam, route recording, remote selected-world graph delivery, Favorite/Stash destinations, structured inventory/equipment state, isolated equipment sets, and fail-closed inventory rule planning.

The combined Phase 2 assistant batch adds Recovery, Combat, Party, Communication, and Diagnostics views. A selected-world/character `AUTOMATIONSTATE` snapshot supplies vitals, learned spell identities, effects, visible current-room NPCs/players, party state, and meditation eligibility. Recovery and combat plans use ordinary server commands; preview/run-once and opt-in continuous execution enforce fresh state, exact room identity, command pacing, Emergency Stop, and bounded repeated attempts. Settings are isolated by world and character. That batch passed 70/70 client-core checks and the 28/28 full validator, including a server snapshot smoke using disposable saves.

The subsequent local Phase 3 work adds a read-only selected-world `CHARACTERSTATE` snapshot and a usable Avalonia Character tab for summary, attributes/skills, spells/effects, progression, and equipment sets. The client combines only matching world/character/room snapshots and clears them on profile or connection changes. Progression shows Host-supplied level-band XP, next-promotion CP, available CP, lives, per-class level/experience, derived statistics, resistances, and saving throws; no progression formula is duplicated in the client. For RealmGate sessions, the equipment planner searches the Host's exact selected-world item catalog with a 75-result cap and compares a hypothetical loadout against current equipped definitions by ID. For external BBS profiles, it reads the existing read-only effective projection with manual override -> imported MDB delta -> Classic precedence, including imported-only records. Current equipment and character observations are accepted only through bounded native terminal parsers and never create qualified RealmGate automation authority. Advanced RealmGate combat planning adds ordered action rules, cooldowns, defensive actions, and default/multi-target/defensive equipment rotation through the existing inventory planner. Party planning adds same-room cure/rescue, explicit regroup, configured visible-player invitations, and preferred rank. Learned-spell actions accept only the Host-reported spell list, buff maintenance uses reported expiry windows, and Diagnostics reports connection/session counters and exports a redacted JSON support snapshot without terminal text or commands. Focused verification passes 79/79 client-core checks; the transitional WPF Release build passes with 0 warnings and 0 errors and recovery/diagnostics smokes exit cleanly. Avalonia passes with 0 errors and the two known SDK/analyzer-version warnings. Native Linux GUI, pushed CI, a real external-board field test, and end-to-end Character-tab interaction remain unverified.

Creator Studio Phase 4 now uses cross-platform `RealmGate.Creator.Core` and `RealmGate.Mdb.Core` assemblies. The Custom-world editor service, administration service, form catalog, managed Access reader, protected MDB/WCC bridge, and performance tracing build outside WPF; Creator import and external-BBS overlay workflows remain distinct. Avalonia exposes a lazy-loaded Custom1 browser plus read-only global search, where-used references, keyed-door lookup, monster/spawn indexing, control-room indexing, capability parity audit, and native MDB/file and WCC/WGS-folder selection. Search results open through the existing Custom1 editor. The full-world search now streams unchanged Librarium records and merges only actual overrides, avoiding the prior full-world clone cost. Guarded maintenance covers bulk Custom1 field set/copy, stopped-world room currency, generated-stock shop reset, dynamic room-stack cleanup, and preview-first textblock replacement. Custom1 world settings, realm-scoped character administration, and Classic local policy/SYSOP grants are also available. Settings and policy changes require exact `CUSTOM1` or `CLASSIC` confirmation, character deletion requires the exact display name, all administration mutations require both embedded Hosts stopped, and override edits invalidate prior validation. Classic gameplay data remains immutable. Selection performs read-only analysis and evidence inventory. Pair validation requires native family coverage and an exact unique source anchor, and import is disabled until that passes and the user acknowledges the Custom1-only boundary; the bridge revalidates before writing and never targets Classic. Shared form definitions drive create/edit fields, saves write changed fields to Custom1 overrides, effective-world validation lists issues, and standalone generation is blocked while errors exist. Portable packages support scoped export plus preview/acknowledge/apply import with the shared pre-import backup. Room connections remain map-qualified and shop inventory uses the shared stock model. The dedicated NPC editor manages attack and ability collections without raw JSON, preserves unknown decoded row fields, permits repeated native ability IDs, and saves only Custom1 overrides. Executable quest stages and linked scripts now have a dedicated view; script saves accept only current runtime-bound opcodes, preserve decoded metadata, rebuild executable clauses/source text, and refuse unsupported or newly invented random-table behavior. NPC and weapon calculators call the recovered runtime resolver. The visual map renders effective room/exit relationships, supports zoom/pan/direct room dragging, keeps numeric coordinate editing, and requires explicit save; gameplay data remains separate. Build now targets isolated `Data/TestRealms/Custom1`. Creator Studio can activate it under the distinct `Custom1-Test` runtime identity with separate characters and world state in `Saves/Custom/custom1-test`; the lifecycle smoke verified projection activation, both listeners, authority routing, save isolation, and graceful shutdown. A reviewed publish revalidates the projection and fingerprint, requires acknowledgement and a stopped embedded Host, stages an exact live swap, audits it, and retains rollback data. Creator regression passes 30/30. The current Test Realm generated 44 families, 112,691 objects, and 60 files; all 60 files passed read-only projection validation. Live Custom1 was not published, and verification changed no policy, character, or Custom1 data. Manual Test Realm gameplay review, a real reviewed live publication, and a real matching-source import/runtime test remain open.

The Avalonia Librarium placeholder is now a lazy read-only browser over generated Classic runtime data. It exposes friendly category tabs, name-only record lists, and curated gameplay details while hiding raw JSON and evidence/provenance metadata. Chunked room/exits projections and string-backed native item fields are validated by the 30/30 Creator Core regression. Cross-platform application defaults are also complete: the existing Server surface persists the default world/profile and Host network limits through a shared Windows/XDG store without restoring the retired Settings tab or theme selector, and preserves unknown WPF fields. Secure credentials now use one file-free platform abstraction: Windows Credential Manager on Windows and Secret Service through `secret-tool` on Linux. Existing Borin/NVIDIA target names and migration are preserved. Real Linux keyring interaction remains unverified.

Cross-platform tester release packaging is now defined for the Avalonia desktop. A dedicated
native-runner workflow produces self-contained Borin-free Windows and Linux archives, verifies
world regeneration from the exact staged compact Librarium, excludes generated `Data/`, includes
checksums and start instructions, and can attach both archives to a GitHub prerelease. A passing
package build establishes archive integrity, not native Linux UI certification. Real Linux window,
terminal, embedded-Host, Secret Service, and gameplay checks remain required from the tester.

The local Windows package gate passed. Its single `RealmGate` root contains 264 entries and is
83.39 MB; the application, start instructions, license notices, configuration, and compact
Librarium are present, while Borin/private dependency names and generated world files are absent.
The staged compact Librarium contains 57 families and 114,630 objects. Before archive creation it
regenerated both Classic and clean Custom1 at 44 families, 112,609 objects, and 60 files each, then
removed disposable `Data/`. The transitional WPF build remained clean at 0 warnings / 0 errors.
The Linux native-runner package and real Linux operation are not yet observed.

This is a usable RealmGate-host assistant slice, not full WPF feature parity. External-BBS automation authority remains deliberately gated until real-board evidence can support it. Native Linux GUI interaction still needs verification on Linux; a Windows cross-publish alone does not establish that result. Creator Studio now has a direct isolated Test Realm session and a passing lifecycle smoke; it still needs manual gameplay review, real publish certification, and matching-source certification.

Implemented areas include terminal/session handling, profiles, Character Workshop, game-data overlays, aliases/macros/triggers/events, navigation, inventory/equipment automation, recovery/buffs, combat automation, party coordination, communications, creature intelligence, diagnostics/customization, reporting, and help.

RealmGate client behavior is implemented through its own WPF/.NET 8 architecture, naming, session state, Librarium/data authority, workflows, and presentation. Client automation must remain independently implemented from RealmGate requirements and observed MajorMUD protocol behavior.

## Release identity and trust

RealmGate now has an embedded multi-resolution `RG` application icon with red faces, a black beveled outline, and a transparent background. The Windows executable also carries explicit RealmGate product metadata and an `asInvoker` application manifest.

`Tools/Release/Publish-RealmGate.ps1` is the supported local publisher for the complete RealmGate app. It preserves the framework-dependent, unpacked release layout, supports certificate-store signing by thumbprint, applies SHA-256/RFC 3161 signatures to first-party RealmGate binaries, and verifies them after signing. The current locally published executable is still **unsigned** because no trusted code-signing certificate has been supplied; Windows reputation warnings cannot truthfully be declared resolved until a trusted signature is applied and the signed artifact is tested.

## Creator Studio state

Creator Studio supports Custom-world authoring/import with source provenance.

Custom-world import policy:

- Classic is immutable.
- MDB is a structured import source, not authority by itself.
- A complete custom-world source import requires the MDB plus associated WCC/WGS native files.
- Source files are SHA-256 fingerprinted and combined into source-pair provenance.
- Imported/changed/new/deleted records are validated before application.
- Custom-world provenance is preserved with the world/export.

`Tools/NightmareBridge/Write-NightmareMdb.ps1` remains required for Custom-world MDB export. The obsolete read bridge has been retired.

## Remaining verification

Automated validation is complete for the current baseline. Two real-file integration tests remain before treating those import paths as field-certified:

1. **External BBS MDB test** — connect the client-side overlay/import workflow to a real non-RealmGate board MDB and verify compare/import/effective-data behavior.
2. **Creator Studio custom-world test** — import a real custom-world MDB together with its matching WCC/WGS source set, review validation/conflict/provenance output, generate the custom world, and test it in runtime.
3. **Signed beta artifact test** — publish with a trusted Windows code-signing certificate, verify the Authenticode chain/timestamp, and test the exact distributed artifact with Microsoft Defender SmartScreen.

These are integration certification tasks, not open Classic parity assertions.

The room/dialogue audit has zero actionable discrepancies. The only source records intentionally left without an executable command are 16 verified `textblock.0` no-target sentinels and `exit.map11.40.w`, whose native command text is genuinely empty; no behavior or alias is fabricated for them. See `Reports/RuntimeVerification/classic_room_command_audit.md`.

## Cleanup state

Historical pass/update documents, old handoffs, duplicated release notes, and obsolete one-off reports can be removed once no active runtime/tool consumes them.

Keep active source, Librarium authority, current validators/tests, required Borin parity scorecard, third-party notices, the MDB writer bridge, and any specialized documentation that is still intentionally used.

## Next gate

Produce the first native Windows/Linux Avalonia tester archives and complete the Linux field pass. Continue Creator Studio Phase 4 with manual gameplay review in the isolated Test Realm, then perform a reviewed real publication and rollback exercise. Final installer/AppImage work, full Host management, and secure Linux credential certification remain migration gates. External-BBS automation authority, the real external-board MDB/session integration test, and the Creator Studio MDB + matching WCC/WGS import/runtime test remain separate field-certification gates.

The 29/29 validator does not need to be rerun merely because documentation is consolidated. Rerun build/smokes after source changes, and rerun the full validator when runtime/data/validator authority changes. Local success does not establish a GitHub Actions result until the corresponding commit is pushed and the workflow is observed.
