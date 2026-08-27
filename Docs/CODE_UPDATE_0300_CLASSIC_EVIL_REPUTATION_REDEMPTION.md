# Code Update 0300 — Classic Evil-Point Reputation & Redemption (Pass 29)

## Scope

Pass 29 closes the numeric MajorMUD Classic Evil-Point reputation reference points and makes the retained native cleanup defaults executable without expanding unresolved law-enforcement behavior.

## Evidence promoted

Recovered MajorMUD operator documentation defines these reference ranges:

- Saint: `-220 <= EP < -200`
- Good: `-200 <= EP < -50`
- Neutral: `-50 <= EP < 30`
- Seedy: `30 <= EP < 40`
- Outlaw: `40 <= EP < 80`
- Criminal: `80 <= EP < 120`
- Villain: `120 <= EP < 210`
- Fiend: `210 <= EP < 300`
- At/over 300 EP the character remains a Fiend but cannot initiate another attack until EP falls below 300.

Retained WCCMMUD setup/message evidence independently carries the Classic defaults:

- `MAXEPDAY = 18`
- `EPCYCLE = 60` minutes
- `EPAMOUNT = 1`

The recovered operator documentation defines cleanup forgiveness as EP reduction, so lower EP moves toward Good/Saint. A 2026-08-27 OmniMUD Classic capture at `237 EP` displaying `FIEND` and `28 to Villain` corroborates the evil-side direction and thresholds but is not treated as native MajorMUD proof by itself.

## Runtime changes

- Added decoded `reputation_minimum_evil_points` to generated character-foundation authority.
- Corrected `maximum_daily_reduction` from descriptive text to integer `18`; the old decode path could turn this into an unlimited cap.
- Added the threshold map to `RuntimeCharacterAlignmentRuleContract`.
- Added authoritative EP-to-reputation resolution and recalculate reputation after ordinary EP changes, attacks, SYSOP `ADD EVIL`, and alignment-cleanup resolution. Administrative `ADD EVIL` does not falsely mark the character as having performed a player evil action that day.
- `RuntimeCharacterAlignmentCleanupConfiguration.ClassicDefault` now uses the retained native maximum of `18`.
- The 300-EP attack-initiation lockout remains separate from the Fiend reputation transition.

## Deliberately unresolved

Pass 29 does **not** promote the following:

- exact world-clock/day-boundary invocation or ordering of user cleanup;
- exact `FORGIVE` history/window accounting;
- complete Lawful creation/action admission semantics;
- Villain/Fiend extra-enforcement actor/cadence, because retained native player help and archived operator documentation conflict;
- guard spawning/routing, arrest, jail, fines, confiscation, or reporting;
- a new reputation below the documented Saint reference floor.

No speculative bounty-hunter/divine enforcement was added to close the score.

## Parity result

- PASS: 76
- STRONG: 76
- UNKNOWN: 28
- Total: 180
- Strict parity: 42.2%
- Evidence-backed: 84.4%
