# CodeUpdate 0083 Timing Handoff

Current best model:

- The host owns scheduling.
- MajorMUD uses RTKICK/_MY_RTKICK and background routines.
- MBBSEmu confirms RTKICK seconds-delay semantics, RTIHDLR 18Hz interval-handler semantics, SYSCYC/INITASK callback surfaces, and a one-second GSBL TICKER.
- Original WCC documentation confirms at least one 3-second MajorMUD interval: page/global-message throttling.
- MMUD Explorer conflicts with the 1.5-second combat-round hypothesis by using `ROUND_SECS = 5` and `SPELL_ROUND_SECS = 3` in its model.

Next best work:

1. Locate the standalone MegaMUD master/config archive if available.
2. Search for `CommandDelay`, `CombatPulse`, `TickSyncBuffer`, `MegaMUD.ini`, or equivalent constants.
3. Map MajorMUD disassembly delay variables (`word_998F6`, `word_998E8`, `word_998EA`, `word_998F2`, `word_998F4`, `word_9993C`, `word_99988`) to their initialized numeric values.
4. Trace `_BACKGROUND_ENERGY`, `_FAST_UPDATE_CHARACTER(S)`, `_FAST_UPDATE_MONSTER(S)`, and spell-duration decrement paths.
5. Promote only after the constant and its consumer are both proven.
