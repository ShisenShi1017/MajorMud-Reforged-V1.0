# MajorMUD Reforged Project Status

## Current position

**CodeUpdate0142 - RealmGate Client and Character Integration**

Database version: `0010`

CodeUpdate0142 closure status: the RealmGate app Release build and the full solution build both pass with 0 warnings / 0 errors. The current-health bootstrap validator also passes with 0 issues / 0 warnings / 0 errors. Client/parity smoke closure remains pending before marking 0142 automated validation complete.

The repository has reached a clean automated verification baseline after the Classic parity closure, Reforged client expansion, Creator Studio MDB/WCC provenance work, and repository cleanup.

## Verified automated baseline

Current locally observed results:

| Gate | Result |
| --- | --- |
| Reforged.App Release build | PASS — 0 warnings, 0 errors |
| Reforged.Server Release build | PASS — 0 warnings, 0 errors |
| Current-health validator | PASS — 26/26 |
| Reforged client smoke suite | PASS — 14/14 |
| Classic parity ledger | 200 PASS / 0 STRONG / 0 UNKNOWN |
| Decode exhaustion in the passing validator run | 48/48 families connected; 2,312/2,312 scalar fields connected; 0 unresolved |

The client smoke suite covers shell/layout, cleanup scheduling, embedded host, automation, map/Librarium, Batch 3 client behavior, inventory automation, recovery automation, combat automation, party automation, diagnostics, parity completion, and help.

The smoke fixes immediately before 0141 corrected test/cleanup assumptions rather than introducing alternate gameplay behavior:

- invalid-regex smoke now verifies the rejected regex trigger itself instead of requiring every other automation rule to remain silent;
- wildcard capture smoke isolates an unrelated PvP rule that could legitimately win the same-priority dispatch;
- Batch 3 treats the retired local-only MegaMUD `.mp` archive as optional and still validates it if a developer restores that archive;
- client smoke diagnostics can write exact failures to generated local report files.

## Runtime/data state

Classic gameplay data is authoritative through `Librarium/` and generated runtime `Data/`.

The accepted Classic parity ledger is:

**200 PASS / 0 STRONG / 0 UNKNOWN = 200**

Reforged remains independent of a live Worldgroup installation for normal runtime. Original native/WCC/WGS/WBB/imported material is evidence or import input, not a runtime dependency unless a specific workflow explicitly consumes it.

Borin's current parity preflight still consumes:

`Reports/RuntimeVerification/majormud_parity_scorecard.json`

Do not delete that file as part of report cleanup without first changing Borin's trainer.

## Client state

The Reforged client capability expansion is automated-smoke clean at 14/14.

Implemented areas include terminal/session handling, profiles, Character Workshop, game-data overlays, aliases/macros/triggers/events, navigation, inventory/equipment automation, recovery/buffs, combat automation, party coordination, communications, creature intelligence, diagnostics/customization, reporting, and help.

Capability parity does not mean copied MudPlay UI. Reforged remains WPF/.NET 8 and uses Reforged architecture, naming, data authority, workflows, and presentation.

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

1. **External BBS MDB test** — connect the client-side overlay/import workflow to a real non-Reforged board MDB and verify compare/import/effective-data behavior.
2. **Creator Studio custom-world test** — import a real custom-world MDB together with its matching WCC/WGS source set, review validation/conflict/provenance output, generate the custom world, and test it in runtime.

These are integration certification tasks, not open Classic parity assertions.

## Cleanup state

Historical pass/update documents, old handoffs, duplicated release notes, and obsolete one-off reports can be removed once no active runtime/tool consumes them.

Keep active source, Librarium authority, current validators/tests, required Borin parity scorecard, third-party notices, the MDB writer bridge, and any specialized documentation that is still intentionally used.

## Next gate

Complete the real external-board MDB integration test first. Then complete the Creator Studio MDB + matching WCC/WGS import/runtime test.

The 26/26 validator does not need to be rerun merely because documentation is consolidated. Rerun build/smokes after source changes, and rerun the full validator when runtime/data/validator authority changes.
