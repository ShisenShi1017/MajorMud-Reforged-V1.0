# CodeUpdate 0084-0087 — Call Graph, Binary Correlation, MegaMUD Provenance, End-to-End Timing

## Scope

This combined update completed the planned 0084-0087 timing lockdown sequence:

- **0084 — Call Graph Reconstruction**
- **0085 — WCC/WBB Binary Correlation**
- **0086 — MegaMUD Provenance Audit**
- **0087 — End-to-End Timing Model**

The Librarium remains canonical. No runtime formulas or generated runtime data were replaced.

## 0084 — Call Graph Reconstruction

MBBSEmu provides the strongest readable host-loop evidence. The observed host order is:

1. Per-session state dispatch.
2. `STTROU` / entering-module / command handling.
3. `STSROU` when user status requires status/output processing.
4. `BEGIN_POLLING` channel routine if registered.
5. `ProcessRTKICK()`.
6. `ProcessRTIHDLR()`.
7. `ProcessSYSCYC()`.
8. `ProcessTasks()`.

This supports the host/module timing model. MajorMUD does not appear to own an independent modern game loop.

## 0085 — WCC/WBB Binary Correlation

The imported WCC and WBB MajorMUD DLLs both expose the same key timing/gameplay symbols in strings output:

- `_begin_polling`
- `_rtkick`
- `_syscyc`
- `_my_rtkick`
- `_majormud_syscyc`
- `_background_fast`
- `_background_medium`
- `_background_slow`
- `_background_energy`
- `_background_inactivity`
- `_background_monster_create`
- `_background_other`
- `_background_save_buffers`
- `_background_update_exits`
- `_do_autocombat`
- `_do_autocombat_for_user`
- `_restart_autocombat`
- `_delay_done`
- `_add_delay`
- `_add_delayed_command`
- `_ljngame_stsrou`
- `_ljngame_sttrou`

This supports the user's claim that the WCC/WBB imports represent equivalent MajorMUD module content under different naming/layout conventions, at least for the timing-relevant symbol layer.

Important contextual strings were also found, including rest/mana regeneration, combat round warnings, autocombat debug strings, room delay strings, and inactivity-in-rounds strings.

## 0086 — MegaMUD Provenance Audit

No original MegaMUD configuration/source file containing the exact strings `CommandDelay`, `CombatPulse`, or `TickSyncBuffer` was found in the currently available repository tree during this pass.

Available related assets include:

- Imported `MegaMudMP` path files.
- MMUD Explorer source and documentation.
- MMUD Explorer MegaMUD pathing UI.
- Existing MegaMUD2RE checkpoint documentation.

Result: the 1500 ms / 3000 ms MegaMUD excerpt remains a candidate until original-file provenance is located.

## 0087 — End-to-End Timing Model

The current best model is:

```text
Hardware/host timing base
        ↓
MajorBBS / Worldgroup host loop
        ↓
Session state dispatch / STTROU / STSROU
        ↓
BEGIN_POLLING channel callbacks
        ↓
RTKICK delayed callbacks
        ↓
RTIHDLR interval callbacks
        ↓
SYSCYC / MajorMUD system cycle
        ↓
MajorMUD background buckets
        ├── background_fast
        ├── background_medium
        ├── background_slow
        ├── background_energy
        ├── background_inactivity
        ├── background_monster_create
        ├── background_other
        ├── background_save_buffers
        └── background_update_exits
        ↓
Gameplay systems: combat, regen, AI, spawn, output
```

## Decisions

- Host/module timing model is now **corroborated**, not fully Classic-verified.
- WCC/WBB timing-relevant symbol equivalence is **observed**.
- Exact combat/recovery constants remain unresolved.
- No formula promotion was made.
- No runtime replacement was made.

## Remaining high-value gaps

1. Determine cadence values for `_background_fast`, `_background_medium`, and `_background_slow`.
2. Map `_do_autocombat` and `_restart_autocombat` to actual scheduler/delay values.
3. Locate primary MegaMUD timing config/source provenance for 1500 ms and 3000 ms values.
4. Determine whether the WCC-documented 3-second global interval also governs regen/spell ticks.
5. Confirm output flush granularity in the original host.

## Status

**PASS_WITH_LIMITATIONS**

The execution model is much stronger, but exact Classic timing constants are not promoted yet.
