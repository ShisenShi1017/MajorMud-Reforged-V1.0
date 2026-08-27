# Classic Evil / Legal System — User-Provided Documentation Evidence

## Status

Validation evidence supplied by the project owner from the archived MajorMUD evil/legal-system documentation. Promoted into the Character Foundation alignment rules for CodeUpdate0137.

## Verified rules

- New characters begin at 0 evil points, reputation Neutral.
- Attacking a Saint or Good target results in 2 evil points.
- Attacking a Neutral, Seedy, Outlaw, Criminal, Villain, or Fiend target results in no evil points from the target-reputation attack rule.
- At 300 or more evil points, a Fiend cannot attack again until evil points fall below 300.
- During cleanup, a character who performed an evil action that day has evil points reduced by 2.
- During cleanup, a character with no evil action that day receives `3 + floor(minutes_played / EPCYCLE) * EPAMOUNT` forgiveness, subject to `MAXEPDAY`. Defaults shown are EPCYCLE=60 and EPAMOUNT=1.
- Native help subsequently closes the Lawful-target multiplier at exactly `3x` the otherwise-applicable evil-point attack penalty. This supersedes the earlier unresolved note while preserving the original operator evidence for the base reputation table.
- Pass 29 / Update 0300 promotes that operator reputation table into the decoded runtime contract and closes the native default cleanup configuration (`MAXEPDAY=18`, `EPCYCLE=60`, `EPAMOUNT=1`). See `CLASSIC_EVIL_REPUTATION_REDEMPTION_PASS_0300.md`. Enforcement wording for Villain/Fiend remains separately evidence-gated because retained native help and the archived operator text do not name the same escalation actor at the same band.

## Critical interpretation correction

The value 2 is not promoted as a universal cost for every possible evil action. It is the documented result for attacking targets in Saint or Good reputation bands.
