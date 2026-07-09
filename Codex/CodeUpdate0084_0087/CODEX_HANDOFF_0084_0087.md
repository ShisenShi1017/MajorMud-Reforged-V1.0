# Codex Handoff — CodeUpdate 0084-0087

## Current baseline

CodeUpdate 0084-0087 completed call graph reconstruction, WCC/WBB binary correlation, MegaMUD provenance audit, and the end-to-end timing model.

## Canonical rule

The Librarium remains the source of truth. Runtime, server, simulator, client, and documentation consume Librarium knowledge. External code and imported binaries are evidence only.

## What changed

New artifacts:

- `Librarium/HostCallGraph0084/host_module_call_graph_0084.json`
- `Librarium/BinaryCorrelation0085/wcc_wbb_binary_correlation_0085.json`
- `Librarium/MegaMUDProvenance0086/megamud_provenance_audit_0086.json`
- `Librarium/EndToEndTimingModel0087/end_to_end_timing_model_0087.json`
- `Evidence/CodeUpdate0084_0087/wcc_wbb_binary_timing_symbol_hits_0085.json`
- `Evidence/CodeUpdate0084_0087/repo_timing_text_hits_0084_0087.json`
- `Tools/Librarium/timing_symbol_trace_0084_0087.py`
- `Reports/CodeUpdate0084_0087/CodeUpdate0084_0087_CallGraphBinaryMegaMUDEndToEndTiming_Report.md`
- `Validation/CodeUpdate0084_0087/validation_0084_0087.json`

## Important finding

The imported WCC and WBB MajorMUD DLLs both expose the same timing-relevant symbols, including `_rtkick`, `_syscyc`, `_majormud_syscyc`, background buckets, autocombat routines, delay routines, and STTROU/STSROU entries.

## Limitation

No original MegaMUD file containing `CommandDelay`, `CombatPulse`, or `TickSyncBuffer` was found in the repo during this pass. The user-provided excerpt remains a candidate until provenance is located.

## Next recommended update

**CodeUpdate-0088 — Timing Promotion Review and Cadence Disassembly Prep**

Recommended goals:

1. Prioritize background bucket cadence recovery.
2. Use symbol names from WCC/WBB to guide disassembly or binary analysis.
3. Map `_my_rtkick`, `_majormud_syscyc`, `_background_*`, and `_do_autocombat` to constants/callers.
4. Promote only host/module model records that meet evidence requirements.
