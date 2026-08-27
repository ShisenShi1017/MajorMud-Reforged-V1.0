# Character Alignment and Evil Points Pass 0143

## Promoted behavior

- New characters begin at `0` Evil Points with reputation label `Neutral`.
- Attacking a `Saint` or `Good` target adds exactly `2` Evil Points.
- Attacking a `Neutral`, `Seedy`, `Outlaw`, `Criminal`, `Villain`, or `Fiend` target adds `0` Evil Points under the recovered target-reputation rule.
- At `300` or more Evil Points, a character cannot initiate another attack until Evil Points fall below `300`.
- Daily cleanup after an evil action reduces Evil Points by `2`.
- Daily cleanup without an evil action uses `3 + floor(minutes_played / 60)` under default `EPCYCLE=60` and `EPAMOUNT=1`, capped by `MAXEPDAY`.
- When Evil Points are already negative, the documented base two-point portion is not applied.

## Runtime ownership and persistence

`RuntimeCharacterAlignmentState` owns Evil Points, the preserved reputation label, the daily evil-action marker, minutes played for cleanup, and the last cleanup amount. Save version 3 persists this state while versions 1 and 2 remain readable.

## Later native closure / still unresolved

Native help now proves the Lawful-target attack modifier is exactly `3x` the otherwise-applicable evil-point attack penalty. Pass 29 / Update 0300 closes the documented Evil-Point reputation reference points from the recovered MajorMUD operator documentation: Saint `-220`, Good `-200`, Neutral `-50`, Seedy `30`, Outlaw `40`, Criminal `80`, Villain `120`, and Fiend `210`. The source documents ranges from `-220` through the Fiend band; behavior below the Saint reference floor is not promoted as a separate native reputation. Villain/Fiend enforcement actor/cadence remains source-conflicted and therefore unresolved.
