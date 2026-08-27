# Classic REST / MEDITATE Regeneration — Pass 37 / Update 0308

Classification: **STRONG stock Classic regeneration boundary**, with direct native HELP proof for activation/interruption semantics.

## Direct native evidence

Exact `WCCMMHLP.mcv` message 205 proves that REST increases HP regeneration, MEDITATE increases Mana regeneration, both require at least **20 uninterrupted seconds** before the increase takes effect, and moving, attacking, or being attacked during that period terminates the state with no bonus.

The WCC/WBB binary correlation also retains `RESTING - REST REGEN %d.` and `MEDITATE - MANA REGEN %d.` strings. Those strings corroborate native ownership but do not independently prove formula or cadence.

## Strong recovered formula boundary

Retained MMUD Explorer source evidence separates stock MajorMUD from GreaterMUD:

- Stock HP base: `max(1, Fix(((Level + 20) * Health) / 750))`.
- GreaterMUD changes the divisor to `/500`; that branch is **rejected for Classic**.
- REST multiplies the stock HP base by **3**, then HPRegen percentage is applied.
- Mana types 1-4 choose INT/WIL/average(INT,WIL)/CHA, then use `/1650`.
- For types 1-4, MEDITATE returns the base before MPRegen percentage; passive regeneration applies MPRegen.
- Kai/type 5 uses a one-point base with MPRegen percentage and bypasses the Level/MagicLevel calculation.

The upstream MMUD Explorer `modMMudFunc.bas` blob used for verification has SHA `1d7981a0edcf9312a89f935277541a16fad81b4d`. Relevant formula lines are also retained in `phase1_exhaustive_evidence_hits.csv`.

## Cadence boundary

Strong retained timing evidence supports:

- passive HP/mana: **30 seconds**
- focused REST HP: **20 seconds**
- focused MEDITATE mana: **10 seconds**
- activation delay for REST/MEDITATE bonus: **20 seconds** (direct native HELP proof)

Exact Worldgroup host-clock phase and simultaneous event ordering remain unresolved.

## Runtime correction

Pass 37 corrects Reforged Classic to the stock branch. The prior live REST implementation used the GreaterMUD `/500` divisor and omitted the stock `x3` resting multiplier. The mana path also applied MPRegen during types 1-4 MEDITATE even though the recovered stock branch exits before that percentage stage.

`native.rest-inn-recovery-policy` therefore moves **UNKNOWN -> STRONG**, not PASS. Exact scheduler phase, exact MEDITATE presentation text, and remaining interruption/status-cleanup ordering stay evidence-gated.
