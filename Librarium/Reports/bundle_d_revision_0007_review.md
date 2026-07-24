# Bundle D Revision 0007 — Full Evidence Review and Provenance Correction

## Review scope

Reviewed cumulative Bundle D revisions 0001 through 0006 against the uploaded repository, Bundle C Revision 0011, generated Classic data, CompilerSmokeTest exports, Librarium behavior graphs, imported WCC/MDB reference edges, native disassembly excerpts, runtime consumers, audits, and installer contents.

## Confirmed correction

The decoded source ability is **Del@Maint**, ability **119**. Native text evidence labels its behavior **Delete at cleanup**. `DeleteAtCleanup` remains an acceptable internal runtime semantic property, but reports and provenance must not claim that the decoded source field is named `del@cleanup`.

Evidence found in the repository includes:

- `Data/CompilerSmokeTest/abilities.json`: ability 119 named `Del@Maint`.
- `Data/CompilerSmokeTest/ClassicNativeRuntimeAudit0139/items.json`: item ability references named `Del@Maint`.
- `Librarium/BehaviorGraph/ability_ownership_lifecycle_graph_0153.json`: `ability.119`, `Del@Maint`.
- `Evidence/Validation0003/MAJORMUD_DISASSEMBLY_EVIDENCE_EXCERPTS.md`: native string `Delete at cleanup`.
- Imported WCC expected-runtime edges map item ability names to `Del@Maint`.

## Phase review status

### D1 World Authority Audit — REOPENED

The audit inventories property names from generated `rooms.json` and `exits.json`, but does not exhaust every decoded/imported/native world source. Its recursive collector only descends through `records` or `data`, so nested source, relationship, validation, metadata, and imported fields are not fully inventoried. A generated-schema audit is useful, but it is not the required all-source world authority audit.

### D2 Rooms — REOPENED

Canonical room identity and metadata retention changes are useful. However, the report explicitly says terrain, region, coordinates, ownership, flags, and special properties were absent from the generated schema and therefore not mapped. Bundle D requires searching all decoded data, imports, references, and native evidence before closure. D2 cannot remain complete while those required categories are deferred solely because the generated room schema lacks them.

### D3 Exits — REOPENED

Canonical exit lookup and destination resolution are useful. The audit itself defers door, hidden, conditional, teleporter, toll, cast-transition, and trap effects. Therefore the exit graph foundation is implemented, but the full exit phase is not exhausted. Special/one-way/conditional semantics must be traced through all decoded sources and connected to live consumers before D3 closure.

### D4 Doors — REOPENED

Basic live closed/locked state and commands are present. The report identifies a split authority: `RuntimeDoorAccessService` is the richer policy engine while `RuntimeLivingWorldService` remains the live command and movement owner. That conflicts with the one-runtime-authority requirement. Pick, bash, magical lock, quest lock, broken, linked, reset, and key-consumption behavior remain unpromoted or unresolved. D4 is not complete.

### D5 Search — REOPENED

Player-specific item discovery and visibility filtering are useful. However, the live consumer supplies a default concealment difficulty of `100` for items, exits, and NPCs when evidence is absent. The exact search weighting is listed as unknown, so this default is an invented gameplay constant unless separately proven. See Hidden currently auto-discovers every eligible target and persists that discovery; that behavior also requires direct evidence. Hidden currency and general hidden objects remain deferred. D5 is not complete.

### D6 Cleanup — REOPENED

The runtime cleanup behavior is aligned with the supplied Classic behavior: ordinary room-ground objects are hidden, ability-119 objects are deleted, and deed/upkeep-qualified gang-house retention is enforced. The decoded source provenance was mislabeled and is corrected in this revision. The universal cleanup schedule and reset ordering remain unresolved, so D6 cannot be called fully exhausted until all decoded timing/ownership sources are reviewed. The runtime semantic property `DeleteAtCleanup` is retained because it describes behavior; source provenance is now `Del@Maint` ability 119.

## Cross-cutting audit findings

- Several audits validate source-code text using `Contains(...)` rather than exercising runtime behavior. These checks can prove a token exists but not that the authoritative live path executes correctly.
- Counting one source filename does not prove there is only one execution path or owner.
- Some reports classify required unresolved fields as later-pass work while simultaneously declaring the current phase complete.
- The generated JSON path was treated as the primary search boundary in multiple phases, contrary to the handoff requirement to search WCC, Worldgroup, MDB, native DLL, Librarium, MMExplorer, Nightmare Redux, imported databases, and supplied runtime evidence.
- No local .NET build was performed in the creation environment. The Windows installer remains the actual compile and runtime-audit gate.

## Corrective disposition

Revisions 0001–0006 are retained as historical cumulative changes, but their completion declarations are superseded by this review. Working canonical-room, exit-resolution, basic-door, per-player hidden-item, and cleanup components remain installed. D1 through D6 must be re-exhausted against all evidence before proceeding to D7.

## Files corrected in Revision 0007

- Cleanup audit source-field terminology.
- Bundle C item/persistence/equipment audit provenance for ability 119.
- Bundle D Revision 0005 and 0006 report terminology.
- Installer revision and cumulative installation list.

## Build result

Not executed locally because the environment does not contain the .NET SDK.

## Audit result

Static all-source review completed. Prior D1–D6 completion claims are reopened as listed above.

## Publish result

Performed by `INSTALL_AND_VALIDATE.bat` on Windows after warning-as-error build and cumulative runtime audits.
