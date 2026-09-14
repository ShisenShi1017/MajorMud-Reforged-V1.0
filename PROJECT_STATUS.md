# RealmGate Project Status

## Current position

**CodeUpdate0142 - RealmGate Client and Character Integration**

Database version: `0010`

CodeUpdate0142 is CLOSED. The RealmGate app Release build and full solution build pass with 0 warnings / 0 errors, the current-health bootstrap validator passes with 0 issues / 0 warnings / 0 errors, all 14 client smokes pass, and the pushed GitHub Classic parity closure passes.

The repository has reached a clean automated verification baseline after the Classic parity closure, RealmGate client expansion, Creator Studio MDB/WCC provenance work, and repository cleanup.

Post-closure runtime hardening now keeps world/session failures contained: a generated world that fails to load is quarantined while healthy worlds remain available, a failing Telnet session reports the error without stopping the Host, and Host restart/stop cleans up faulted runtime tasks instead of allowing an exception to close the WPF app. The Host workspace now contains its live Sessions and Logs tabs.

Character deletion is available to account owners inside each selected world's character menu and to editors in Creator Studio's world-aware Characters tab. Both paths enforce account/world ownership and refuse deletion of logged-in characters. The character smoke now verifies create, restore, decoded LIGHT equip, permanent delete, and account-slot cleanup.

## Verified automated baseline

Current locally observed results:

| Gate | Result |
| --- | --- |
| RealmGate app Release build | PASS — 0 warnings, 0 errors |
| Full solution Release build | PASS — 0 warnings, 0 errors |
| Current-health validator | PASS — 26/26 |
| RealmGate client smoke suite | PASS — 14/14 |
| Classic parity ledger | 200 PASS / 0 STRONG / 0 UNKNOWN |
| Decode exhaustion in the passing validator run | 48/48 families connected; 2,316/2,316 scalar fields connected; 0 unresolved |

The client smoke suite covers shell/layout, cleanup scheduling, embedded host, automation, map/Librarium, Batch 3 client behavior, inventory automation, recovery automation, combat automation, party automation, diagnostics, parity completion, and help.

The latest full local validator passed 26/26 on 2026-09-11 Pacific / 2026-09-12 UTC. Its solution build had 0 errors; four `NU1900` warnings were emitted only because NuGet's vulnerability index was unreachable in the restricted local environment. Direct RealmGate App, Server, and validator Release builds each passed with 0 warnings / 0 errors.

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

The RealmGate client capability expansion is automated-smoke clean at 14/14.

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

## Cleanup state

Historical pass/update documents, old handoffs, duplicated release notes, and obsolete one-off reports can be removed once no active runtime/tool consumes them.

Keep active source, Librarium authority, current validators/tests, required Borin parity scorecard, third-party notices, the MDB writer bridge, and any specialized documentation that is still intentionally used.

## Next gate

Complete the real external-board MDB integration test first. Then complete the Creator Studio MDB + matching WCC/WGS import/runtime test.

The 26/26 validator does not need to be rerun merely because documentation is consolidated. Rerun build/smokes after source changes, and rerun the full validator when runtime/data/validator authority changes. Local success does not establish a GitHub Actions result until the corresponding commit is pushed and the workflow is observed.
