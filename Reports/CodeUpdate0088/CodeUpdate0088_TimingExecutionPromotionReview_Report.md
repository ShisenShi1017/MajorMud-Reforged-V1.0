# CodeUpdate 0088 — Timing / Execution Verification Promotion Review

## Scope

CodeUpdate 0088 reviewed the timing and execution evidence produced in 0083 through 0087 and made conservative promotion decisions. The Librarium remains canonical. Runtime data, formulas, and generated output were not changed.

## Promotion result

- Verified promotions: **3**
- Corroborated promotions: **2**
- Formula promotions: **0**
- Runtime formula replacements: **0**

## Records promoted to Verified

### 1. MajorMUD as a hosted MajorBBS/Worldgroup module

Promoted to **Verified** for architecture only. This verifies that MajorMUD is a host-managed module and should not be modeled as a client-owned or independent modern game loop. It does not verify gameplay cadence constants.

Evidence chain:

- WCC/WBB imported MajorMUD artifacts and documentation identify the module environment.
- MBBSEmu models host-managed module dispatch.
- WCC/WBB DLLs expose STTROU/STSROU/BEGIN_POLLING-related symbols.

### 2. WCC/WBB timing-symbol equivalence

Promoted to **Verified** for inspected-binary symbol equivalence only. The WCC and WBB MajorMUD DLLs share the same timing-relevant symbol layer.

Scope limit: this does not prove binary identity or exact timing values.

### 3. MajorMUD background/autocombat/delay symbol presence

Promoted to **Verified** for symbol presence only. Imported DLLs contain background buckets, autocombat routines, delay routines, and system-cycle related symbols.

Scope limit: exact cadence and call ordering are still unresolved.

## Records promoted to Corroborated

### 1. Host-driven scheduling model

Promoted to **Corroborated**. MBBSEmu provides readable host-loop behavior and the imported MajorMUD DLLs expose matching host-facing callback/scheduler symbols.

### 2. Delayed work via RTKICK API

Promoted to **Corroborated**. MBBSEmu implements RTKICK-style delayed scheduling and WCC/WBB DLLs expose `_rtkick` and `_my_rtkick` symbols.

Scope limit: MBBSEmu observes seconds semantics, but original MajorMUD cadence constants still need direct recovery.

## Not promoted

The following remain unverified:

- 1.5-second combat round / `CombatPulse=1500`
- 3.0-second HP/mana/spell/recovery tick
- tick clipping
- background_fast/background_medium/background_slow cadence values
- output flush granularity

## Why not promote the timing constants?

The evidence is now strong enough to verify architecture and symbol presence, but not the exact Classic gameplay constants. In particular:

- The user-provided MegaMUD timing excerpt has not yet been tied to an original file path in the repository.
- MMUD Explorer contains `ROUND_SECS = 5`, which conflicts with a simple 1.5-second combat-round claim.
- WCC documentation proves a 3-second global-message/page window, but not HP/mana/spell/recovery cadence.
- Tick clipping remains logically plausible but not traced or documented.

## Next recommended work

**CodeUpdate 0089 — Cadence Disassembly / Scheduler Cross-Reference**

Focus directly on:

1. `_majormud_syscyc`
2. `_background_fast`
3. `_background_medium`
4. `_background_slow`
5. `_background_energy`
6. `_do_autocombat`
7. `_restart_autocombat`
8. `_add_delay` / `_add_delayed_command`

The goal is to recover actual cadence constants or call relationships from imported binaries/source evidence, not infer them from client timing lore.

## Status

**PASS_WITH_LIMITATIONS**
