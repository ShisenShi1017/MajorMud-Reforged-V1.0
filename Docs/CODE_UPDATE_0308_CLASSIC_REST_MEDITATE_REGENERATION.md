# CodeUpdate0308 / Pass 37 — Classic REST / MEDITATE Regeneration Correction

Pass 37 corrects a live Classic regeneration defect and narrows `native.rest-inn-recovery-policy`.

## Correction

The previous Reforged REST path was effectively using the **GreaterMUD** HP branch (`/500`) and did not apply stock MajorMUD's resting multiplier. Strong recovered source separates the branches explicitly. Classic now uses the stock `/750` HP branch:

`baseHP = max(1, ((Level + 20) * Health) / 750)`

- Passive HP: apply HPRegen percentage to `baseHP`.
- REST HP: multiply `baseHP` by **3**, then apply HPRegen percentage.

Mana types 1-4 retain the recovered casting-stat and `/1650` formula. Passive mana applies MPRegen after that base, while MEDITATE returns the base **before** MPRegen. Kai/type 5 remains the recovered one-point special branch with MPRegen.

## Timing and native HELP

Native WCCMMHLP message 205 directly proves the 20-second activation requirement and that movement, attacking, or being attacked during activation terminates the restful/meditative state with no bonus. Strong retained evidence supports 30-second passive, 20-second REST, and 10-second MEDITATE cadences.

Exact host-clock phase, simultaneous tick ordering, exact MEDITATE response presentation, and remaining cleanup ordering are still unresolved. Therefore the target assertion moves **UNKNOWN -> STRONG**, not PASS.

## Parity

**97 PASS / 78 STRONG / 25 UNKNOWN = 200**

- Strict parity: **48.5%**
- Evidence-backed: **87.5%**
