# Classic Lifecycle / Death Hypothesis Verification — Pass 39 / Update 0310

Pass 39 reviewed the three user-provided pages as **hypotheses**, not native MajorMUD authority.

## Verified / retained

- `EXIT` / `QUIT` uses the already-proven five-second departure delay.
- Graceful exit automatically saves the character.
- Native FOLLOW help states that an explicitly exiting follower stops following; the live Classic hook already applies this for non-leader followers.
- Existing life parity remains intact: normal death costs one life; official help says `SUICIDE` has the same normal-death effect for the life counter, while last-life `SUICIDE` and `SUICIDE NOW` lead to reroll behavior.

These facts are enough to narrow the MajorMUD-owned lifecycle boundary from UNKNOWN to STRONG. They do **not** establish exact unexpected-disconnect semantics or a universal cleanup order.

## Combat-page correction

The supplied `ActionDelay = WeaponSpeed - Agility/20 - HasteSkill/10` priority-queue model is not used for Classic parity. It conflicts with the already-PASS stock attack-energy recovery:

- attack energy is calculated from the recovered Classic equation;
- each physical combat round contributes 1000 action-energy units;
- unused energy carries forward;
- whole swings execute when enough energy is available;
- five swings is the hard cap.

The exact ordering between multiple actors whose attacks are due on the same native cycle is still not recovered. In particular, Pass 39 does not claim that a lethal earlier action always purges a later due swing.

## Linkdeath-page boundary

The following are **not promoted** from the supplied page:

- fixed three-minute LINKDEATH timer;
- same username/IP reconnect rule;
- linkdead autoattack/combat-retention policy;
- timer pause while combat resumes;
- automatic highest-level party-leader succession;
- mercenary serialization/unspawn/return-to-tavern rule;
- charm/summon hostility on logout;
- `player.json` native persistence or save-and-destroy object sequencing.

Raw socket reattachment, account/session ownership and duplicate-session mechanics are Worldgroup host responsibilities and remain excluded from MajorMUD parity. MajorMUD-owned gameplay consequences of unexpected disconnect remain unresolved.

## Death-XP page boundary

The page's level-under-10 immunity, 10% loss, integer truncation, 2,000,000 cap and exact one-level regression claims are **not proven by retained native evidence** and are not implemented.

The existing experience-chart evidence does support one constraint: race/class EXP modifiers construct the experience chart and must not be silently reused as a death-loss multiplier. The exact normal-death XP formula remains UNKNOWN.

## Parity result

`character.native-lifecycle-boundary`: **UNKNOWN -> STRONG**.

`character.death-xp-loss-exact`: remains **UNKNOWN**.

No guessed linkdeath timer, death-XP arithmetic, priority queue, leader succession, reconnect rule, or native JSON save model is introduced.
