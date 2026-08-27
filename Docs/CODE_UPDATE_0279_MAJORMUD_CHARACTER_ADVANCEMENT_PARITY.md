# Code Update 0279 — MajorMUD Character / Advancement Parity

Baseline: `MajorMud-Reforged-V1.0-MajorMUD-Parity-Cumulative-Pass8.zip` plus the Pass 8 BuildRepair1–3 overlays.

## Scope

Pass 9 audits Character / Advancement behavior using recovered/native evidence only. The MajorMUD Complete Mechanics & Systems Guide is not treated as formula authority.

## Activated / corrected behavior

### Magic Resistance — STRONG

All live character-target resistance paths now share one character formula owner:

`Fix((INT + WIL*3) / 4) + modifiers`

This follows retained MMUD Explorer `CalcMR` implementation evidence. An older screenshot-derived note reversed the INT/WIL weighting, so this remains `STRONG` rather than `PASS` until the native DLL ordering is independently recovered. NPC hit-spell resistance, player-vs-player/player-target spells, and decoded failure-branch casts now consume the same total MR authority.

### Raw character Dodge — STRONG

The prior synthetic `AGI / 10` character Dodge placeholder is removed from new-character creation and live combat profiles. Raw Dodge now uses the recovered stock builder:

- `Fix(Level / 5)`
- `+ Fix((Charm - 50) / 5)`
- `+ Fix((Agility - 50) / 3)`
- `+ PlusDodge`
- if encumbrance is below 33%, `+ 10 - Fix(encumbrancePct / 10)`

This raw value feeds the already-proven Dodge-vs-Accuracy calculation and final 95% cap. Race/class ownership of `PlusDodge` is not guessed where decoded ability-slot mapping remains ambiguous.

### Ability/equipment contribution de-duplication — STRONG

The 0155 statistic projector can store acquired/quest ability values and equipment ability values in the same derived total. Several combat consumers were also adding the acquired value directly, allowing it to be counted twice. Pass 9 detects authoritative projected totals and subtracts the already-counted acquired portion before adding the equipment contribution.

The corrected consumers are:

- Dodge
- modified Stealth
- maximum damage Ability 4 contribution
- Backstab minimum Ability 117 contribution
- Backstab maximum Ability 118 contribution
- Magic Resistance Ability 36 composition

Unmarked legacy/test semantic snapshots remain compatible and are treated as equipment-only projected values.

## Confirmed existing PASS behavior

Pass 9 re-audits and explicitly scores behavior that was already present but not granularly represented in the parity ledger:

- native-traced Max HP creation formula, including the duplicate constitution adjustment observed in `wccmmud.dll`
- Max Mana semantics for magic types 1–4 and Kai/type 5
- one-level promotion HP growth and mana growth
- decoded race/class experience percentage modifiers
- verified modified Stealth formula using level, AGI, INT, Charm, class/race ownership, and Ability 27

## Retained STRONG behavior

- normal/resting HP regeneration from Level, Health, and HPRegen
- mana regeneration/meditation from Level, magic type casting stat, MagicLevel, and MPRegen

Exact timer phase/host-clock alignment and the complete type-5/Kai MPRegen branch ordering remain below `PASS` confidence.

## Preserved UNKNOWNs

Pass 9 deliberately does not invent:

- stock Picklocks base formula — retained evidence contains materially different branches
- absolute native XP thresholds and CP award rows — `CalcCPLevel` is retained as a lead only
- exact death/lives behavior, including XP loss, partial gold loss, corpse decay, resurrection ordering, status cleanup, alternate respawn, looting, and lives semantics
- complete cross-category order among race/class modifiers, acquired abilities, equipment, auras/status effects, and stat-derived values

## Validation

Static Pass 9 validation: `22/22` checks passed in `Reports/RuntimeVerification/character_advancement_pass9_static_validation_0279.json`.

Python validators that passed in the packaging environment include Librarium validation, generation readiness, combat runtime validators, Gameplay Group 4, and the WCC Librarium exhaustion audit. Gameplay Groups 1–3 retain their known failures because generated `Data/Classic` files are absent. Additional legacy/economy validators also report pre-existing missing packaged evidence/data inputs.

The packaging environment does not contain the .NET SDK, `csc`, or `msbuild`, so a C# build cannot be executed here. Local `dotnet build` remains the final compile gate.

## Parity accounting

The Pass 9 ledger decomposes Character / Advancement into fourteen additional explicit requirements. This expands the project denominator from 128 to 142:

- PASS: `55 → 60`
- STRONG: `25 → 30`
- UNKNOWN: `48 → 52`
- strict: `43.0% → 42.3%`
- evidence-backed: `62.5% → 63.4%`

The strict percentage decrease is denominator expansion, not loss of an existing PASS. No prior PASS or STRONG assertion was demoted.
