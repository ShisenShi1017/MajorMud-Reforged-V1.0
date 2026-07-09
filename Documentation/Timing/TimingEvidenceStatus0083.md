# Timing Evidence Status — CodeUpdate 0083

The strongest internally supported model is host/module-driven timing:

- MajorBBS/Worldgroup host provides real-time scheduling primitives.
- MajorMUD registers background callbacks through host routines such as `RTKICK` and `_MY_RTKICK`.
- Some MajorMUD systems are background routines rather than client-driven timers.
- Modem latency affects visibility and command pacing, not the authoritative scheduler.

## Constants and state

- `RTIHDLR`: Observed host interval handler; MBBSEmu documents original DOS behavior as 18 Hz.
- `TICKER`: Observed GSBL one-second counter in MBBSEmu.
- `RTKICK`: Observed one-shot delayed callback measured in seconds.
- 3-second interval: Observed in WCCMMUD.DOC for page/global-message throttling; still only Derived for HP/mana/buff recovery.
- 1.5-second combat pulse: Needs provenance from actual MegaMUD source/config or another primary source.
- 5-second combat round: Derived candidate from MMUD Explorer modeling constants.
- Tick clipping: Hypothesis until a runtime trace or primary documentation proves global decrement behavior.

## Promotion rule

Do not promote a timing constant to Verified unless the evidence chain identifies both:

1. the source of the constant, and
2. the subsystem that consumes it.

For example, a 3-second page throttle is not automatically proof of a 3-second regeneration tick.
