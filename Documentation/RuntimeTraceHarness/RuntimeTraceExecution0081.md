# CodeUpdate 0081 — Execute Trace Harness Against Evidence

Status: **PASS_WITH_LIMITATIONS**

0081 executed the 0080 trace harness against the available source evidence rather than against a live Classic MajorMUD runtime. The result is a source-derived trace model for MBBSEmu host behavior.

## Main conclusions

1. **Scheduler cadence is host-controlled.** MBBSEmu uses a `Timer.Hertz` event when idle, but bypasses waiting when session input, output, or processing is pending. This supports host/module-driven timing, not modem-ping-driven gameplay ticks.

2. **Per-session processing occurs before timed callbacks.** The worker loop processes sessions serially, then runs timed/real-time callbacks.

3. **MBBSEmu timed callback order is reconstructed:** `RTKICK` → `RTIHDLR` → `SYSCYC` → `INITASK`/tasks.

4. **RTIHDLR has a ~55ms gate in MBBSEmu.** This is host evidence, not proof MajorMUD uses RTIHDLR.

5. **GSBL `TICKER` increments once per second.** This is a visible host/GSBL timing variable, but not automatically the gameplay tick.

6. **Output flushing remains partially unresolved.** `DataToClient` is a host output queue and pending output can wake processing, but live socket flush order still needs instrumentation.

## Promotions

- Formula promotions: **0**
- Runtime formula replacements: **0**
- Host behavior promotions to Classic Verified: **0**
- Host behavior marked Observed for MBBSEmu source order: **1**

No Classic verification occurred because no live Classic runtime or instrumented module trace was available.

## Updated gaps

All four 0079 host gaps were reduced but not closed for Classic MajorMUD.

- Scheduler cadence: reduced
- MajorMUD callback usage: reduced
- Output flush/perceived tick timing: reduced
- RTIHDLR usage by MajorMUD: reduced

## Next recommendation

CodeUpdate 0082 should add optional instrumentation hooks to MBBSEmu or a companion trace adapter so the source-derived order can be validated with actual runtime logs.
