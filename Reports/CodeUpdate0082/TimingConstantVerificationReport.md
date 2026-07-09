# CodeUpdate 0082 — Timing Constant Verification

Generated: 2026-07-08T21:00:05Z

## Result

Status: **PASS_WITH_LIMITATIONS**

0082 investigated the concrete timing claims surfaced in the user screenshots and compared them against the attached evidence repositories.

## Decisions

- **0 timing constants promoted to Verified**
- **0 formulas promoted**
- **0 runtime formulas replaced**
- **4 timing gaps created/updated**

## Claim Review

### 3.0-second system/spell tick

Status: **Derived candidate, not verified**.

Supporting evidence exists in MMUD Explorer: the changelog references 3-second round ticks for bless calculations, and the monster attack simulator uses a repeating 1..3 duration-tick cadence. The user screenshots also claim a 3.0-second global system tick.

Limitation: no primary MajorMUD/Worldgroup SDK text or executable Classic trace was supplied. MBBSEmu confirms host-driven seconds/timer infrastructure, but does not prove MajorMUD's exact 3.0-second gameplay tick.

### 1.5-second combat round

Status: **Conflicting candidate, not verified**.

The user screenshots claim 1 combat round equals 1.5 seconds and 1 tick equals 2 rounds. However, MMUD Explorer's monster attack simulator contains timing vocabulary that points in a different direction: it describes spell rounds as 3 seconds and combat rounds as 5 seconds in that context. This does not prove the screenshot is wrong, but it prevents promotion.

### Tick clipping

Status: **Plausible hypothesis, not verified**.

The screenshots describe global heartbeat clipping, where an effect cast just before a global tick loses most of its first tick. This is technically plausible for a global tick system, but no primary evidence or trace proves it yet.

### Host-driven timing

Status: **Observed for MBBSEmu; not Classic-verified**.

MBBSEmu provides host-level timer, TICKER, polling, and RTKICK evidence. This strongly supports host/module-driven timing, but the exact Classic MajorMUD constants remain unverified.

## Next Step

CodeUpdate 0083 should target primary/runtime evidence acquisition:

1. Search for MegaMUD source or scripts that hardcode combat timing.
2. Search Reference Pack / historical docs for 3-second tick and 1.5-second combat round language.
3. Run timestamped probes against any executable emulator/module combination.
4. Resolve the 1.5-second versus 5-second combat-round conflict before any promotion.
