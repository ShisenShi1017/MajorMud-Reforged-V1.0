# CodeUpdate 0083 — Timing Internal Evidence Extraction

## Scope

This pass stopped treating timing as an external research problem and searched the repository-owned evidence directly:

- Imported WCC/WBB MajorMUD files under `ImportedDatabases/WCCDefaultInstall` and `ImportedDatabases/WCCLabeledWBB`
- Existing MajorMUD disassembly evidence already imported into `Evidence/Validation0003` and related reports
- MMUD Explorer GitHub source ZIP supplied in this session
- MBBSEmu GitHub source ZIP supplied in this session
- Existing 0082 timing records

No runtime formulas or generated runtime behavior were changed.

## Findings

### 1. Host scheduling is now internally corroborated

MBBSEmu provides direct source evidence for host-provided scheduling primitives:

- `RTKICK` is a delayed one-shot routine with delay measured in seconds.
- `RTIHDLR` is documented as an 18 Hz interval handler from original DOS MajorBBS/Worldgroup behavior.
- `SYSCYC` and `INITASK` are host callback surfaces.
- `BEGIN_POLLING` callbacks are run as often as possible for registered channels.
- GSBL `TICKER` increments once per second.

Imported MajorMUD disassembly evidence shows WCCMMUD registering multiple background routines through `RTKICK` and `_MY_RTKICK`, including `_BACKGROUND_FAST`, `_BACKGROUND_MONSTER_CREATE`, `_BACKGROUND_OTHER`, `_BACKGROUND_SLOW`, `_BACKGROUND_MEDIUM`, `_BACKGROUND_ENERGY`, and `_BACKGROUND_INACTIVITY`.

**Promotion impact:** host/module-driven scheduling remains strongly supported and can be treated as Observed/engine-documentation-level evidence. It still does not by itself prove exact Classic gameplay constants.

### 2. Original WCC documentation confirms MajorMUD polling and at least one 3-second interval

The original WCC sysop/user documentation contains timing controls and statements:

- `INITRATE` controls the rate of MajorMUD initialization.
- `POLLRATE` is a counter used during the polling routine.
- `SECOTH` and `SECBUFF` are second-based persistence/buffer-save intervals.
- `INACTIVE` is second-based inactivity timing.
- The user documentation explicitly refers to a maximum number of pages/global messages in a **3 second interval**.

**Promotion impact:** the 3-second value is no longer only a community/AI-summary claim. It exists in imported WCC documentation for global message throttling. This supports a MajorMUD 3-second window, but it does not yet prove that HP/mana/buff recovery ticks use the same 3-second interval.

### 3. MMUD Explorer conflicts with a universal 1.5-second combat-round claim

MMUD Explorer contains explicit timing constants:

- `ROUND_SECS = 5`
- `SPELL_ROUND_SECS = 3`
- `STOCK_MOB_HPREGEN_ROUNDS = 18`
- `GMUD_MOB_HPREGEN_ROUNDS = 6`

It also comments that `SEC_PER_ROUND` is a combat round, with `5s typical`, in its EXP-per-hour modeling module.

**Promotion impact:** 1.5 seconds cannot be promoted as the universal Classic combat round. It remains a user/MegaMUD-derived candidate requiring provenance. MMUD Explorer supports a different modeling frame: 5-second combat rounds and 3-second spell rounds.

### 4. User-supplied MegaMUD timing excerpt is highly relevant but not yet repository-proven

The provided excerpt:

```ini
[Timing]
CommandDelay=1500
CombatPulse=1500
TickSyncBuffer=3000
```

would be strong MegaMUD-observed evidence if an original file path, archive member, source commit, or documentation page can be tied to it.

Current repository scan did **not** locate `CombatPulse`, `TickSyncBuffer`, or `MegaMUD.ini` in the supplied files. The attached GitHub ZIP visible in this session is `MMUD-Explorer-master.zip`; it contains MegaMUD-related pathing/import artifacts but is not confirmed to be the standalone MegaMUD client source/config repository.

**Promotion impact:** record as `needs_provenance`, not Verified.

## Updated timing statuses

| Claim | New status | Reason |
|---|---:|---|
| Host/module-driven scheduling | Observed | MBBSEmu source + MajorMUD disassembly evidence agree. |
| RTKICK one-shot seconds delay | Observed | MBBSEmu source explicitly documents seconds delay; MajorMUD uses RTKICK. |
| RTIHDLR 18 Hz host interval | Observed | MBBSEmu source documents original DOS MajorBBS/Worldgroup 18 Hz behavior. |
| GSBL TICKER 1 second | Observed | MBBSEmu source implements one-second TICKER. |
| 3-second global message/page window | Observed | Original WCCMMUD.DOC documents a 3 second interval. |
| 3-second HP/mana/buff tick | Derived candidate | Plausible, but WCC proof located so far is global-message timing, not recovery tick proof. |
| 1.5-second combat round | Conflicting candidate | User/MegaMUD claim conflicts with MMUD Explorer 5-second combat-round modeling. |
| 5-second combat round | Derived candidate | MMUD Explorer modeling evidence; not primary engine proof. |
| Tick clipping | Hypothesis | Expected under global tick scheduling, but not reproduced or documented in primary WCC material yet. |

## Files created

- `Evidence/CodeUpdate0083/timing_symbol_scan_0083.csv`
- `Evidence/CodeUpdate0083/timing_evidence_claims_0083.json`
- `Reports/CodeUpdate0083/TimingInternalEvidenceExtractionReport.md`
- `Documentation/Timing/TimingEvidenceStatus0083.md`
- `Validation/CodeUpdate0083/timing_validation_0083.json`
- `Codex/CodeUpdate0083/TIMING_HANDOFF_0083.md`

## Validation result

`PASS_WITH_FINDINGS`

No formulas promoted. No runtime formulas replaced.
