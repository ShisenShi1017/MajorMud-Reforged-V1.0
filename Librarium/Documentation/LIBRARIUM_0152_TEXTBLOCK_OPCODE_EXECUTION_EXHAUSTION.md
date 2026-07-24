# CodeUpdate 0152 — Textblock Opcode Execution Exhaustion

This pass promotes a machine-readable execution contract for every opcode present in the 182 authoritative MMExplorer textblock-stage scripts.

## Promoted behavior

- All 25 opcodes and all 2,616 operations are classified as condition or effect operations.
- Every opcode parameter has an index, name, canonical type, allowed arity, and observed arity.
- Optional failure-message arguments are separated from semantic operands.
- Clause execution is fixed as condition preflight, state capture, ordered effects, first-match return, and complete state rollback on unsafe execution.
- Random tables use an inclusive 1–100 roll, ascending thresholds, and first-threshold selection; invalid or uncovered tables fail closed.
- Character-save, output-only, world-runtime, and native-dependent impacts are recorded per opcode.

## Runtime enforcement

`RuntimeTextblockCommandAbilityCatalog` now requires `contract.textblock.opcode_execution` and rejects missing opcodes, duplicate opcodes, invalid phases, missing parameter schemas, missing arities, count mismatches, incomplete execution ordering, or a non-fail-closed unknown-opcode policy.

## Native-only remainder

Exact native message/text payload rendering and the side effects of `remoteaction 1423 66 0 3` remain explicitly native-dependent. Their opaque references are preserved without invented behavior.
