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
| Cross-platform client-core regression | PASS — 72/72 (latest local Character slice) |
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

The complete transitional WPF client capability expansion retains its accepted 14/14 smoke baseline. Shared terminal, transport, protocol, automation, event, navigation, inventory, assistant planning, and Character snapshot/item-search behavior now has a 72/72 cross-platform client-core regression suite.

The Avalonia Play surface now includes live Telnet/ANSI operation, automation, timed events, verified Goto/Auto-Roam, route recording, remote selected-world graph delivery, Favorite/Stash destinations, structured inventory/equipment state, isolated equipment sets, and fail-closed inventory rule planning.

The combined Phase 2 assistant batch adds Recovery, Combat, Party, Communication, and Diagnostics views. A selected-world/character `AUTOMATIONSTATE` snapshot supplies vitals, learned spell identities, effects, visible current-room NPCs/players, party state, and meditation eligibility. Recovery and combat plans use ordinary server commands; preview/run-once and opt-in continuous execution enforce fresh state, exact room identity, command pacing, Emergency Stop, and bounded repeated attempts. Settings are isolated by world and character. That batch passed 70/70 client-core checks and the 28/28 full validator, including a server snapshot smoke using disposable saves.

The subsequent local Phase 3 Character slice adds a read-only selected-world `CHARACTERSTATE` snapshot and a usable Avalonia Character tab for summary, attributes/skills, spells/effects, experience, and equipment sets. The client combines only matching world/character/room snapshots and clears them on profile or connection changes. The equipment planner now searches the Host's exact selected-world item catalog with a 75-result cap, shows decoded item details, and compares a hypothetical loadout against current equipped definitions by ID. It does not assert class/race/hand eligibility or execute preview equip commands. Its focused verification passed 72/72 client-core checks and the core/server smoke suite; the Avalonia Release build succeeded with two known analyzer-version warnings. The later full 29/29 validator passed after the complete room/dialogue recovery. Native Linux GUI, pushed CI, and end-to-end Character-tab interaction remain unverified.

This is a usable RealmGate-host assistant slice, not full WPF feature parity. Advanced action/equipment rotation, party invitation/regroup/rescue policies, external-BBS automation state parsing, diagnostic exports/session statistics, and richer spell/buff management remain migration work. Native Linux GUI interaction still needs verification on Linux; a Windows cross-publish alone does not establish that result.

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

Finish external-BBS effective item planning, richer Character progression, and the remaining Phase 2 assistant gaps, then move into Creator Studio services. Librarium, full Host management, secure Linux credentials, public packaging, and native Linux UI testing remain later migration gates. The real external-board MDB integration test and Creator Studio MDB + matching WCC/WGS import/runtime test remain separate field-certification gates.

The 29/29 validator does not need to be rerun merely because documentation is consolidated. Rerun build/smokes after source changes, and rerun the full validator when runtime/data/validator authority changes. Local success does not establish a GitHub Actions result until the corresponding commit is pushed and the workflow is observed.
