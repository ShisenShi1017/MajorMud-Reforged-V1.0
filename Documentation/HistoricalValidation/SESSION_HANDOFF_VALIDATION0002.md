# Session Handoff — Validation0002

Current validation baseline: MajorMUD-Reforged-Validation0002-Cumulative.zip

Next valid validation pass: Validation0003

Focus next:
- Analyze `_BACKGROUND_MONSTER_CREATE`.
- Trace `RTKICK` arguments near background routines.
- Map `mcurou`/cleanup entry points in MajorMUD disassembly.
- Connect cleanup and respawn routines to item/monster tables.

Rules:
- `/Data` remains authoritative and unchanged unless direct evidence supports promotion.
- MBBSEmu host evidence may verify host behavior, but MajorMUD semantics need module-level evidence before promotion.
