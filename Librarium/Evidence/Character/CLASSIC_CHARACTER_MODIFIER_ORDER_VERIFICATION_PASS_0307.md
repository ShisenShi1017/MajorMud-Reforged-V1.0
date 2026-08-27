# Classic Character Modifier-Order Verification — Pass 36 / Update 0307

## Purpose

Pass 36 evaluates the two operator-supplied modifier-order document screenshots as **hypotheses**, not native authority. Claims are promoted only where retained native MajorMUD evidence independently corroborates them. Contradicted or unsupported claims remain excluded from Classic runtime parity.

## Native evidence anchors

### Creation base statistics

`CHARACTER_CREATION_PASS_0141.md` is already promoted evidence: a new Classic character starts at **level 1** with the selected **race minimum statistics**. This directly rejects the screenshot's proposed generic base-stat equation `Class Base Stat + (Class Level-Up Gain * Level) + Race Modifier` as a native Classic creation rule.

### Native secondary-stat recomputation

`CHARACTER_HP_MANA_CALC_MUDBRAIN_0137.md` records Ghidra-recovered `wccmmud.dll` function `calculate_secondary_stats @0x0041a424`. The routine is called when stats change, including stat drain and equipment changes. Its exact recovered formulas establish category-specific ordering for Max HP and Max Mana, including current HEA, class/race HP terms, accumulated HP rolls, MaxHP ability cache, and the mana equipment cache. This is an exact per-formula ordering proof, not a proof of one universal pipeline for every statistic.

### Direct ability/stat deltas

The retained Classic ability catalog explicitly defines signed parameter-based stat changes for Ability 45 Wisdom/Willpower and Abilities 46-49 Strength, Health, Agility, and Charm. The catalog describes these as increasing or decreasing the target stat by the supplied value. No equivalent retained Intellect stat-delta ability is asserted by this pass. Nothing in that evidence establishes a general percentage stage for those stat abilities.

### Direct equipment combat modifiers

`CHARACTER_STATISTICS_PASS_0154.md` establishes deterministic additive aggregation for the decoded item fields `armor_class_bonus`, `damage_resistance_bonus`, `accuracy_bonus`, and `speed_bonus`. It explicitly leaves final cross-category ordering unresolved. The retained item projection does not provide evidence for the screenshot's generic "flat equipment, then percentage equipment" stat pipeline.

### Dodge / encumbrance ordering

`majormud_character_advancement_evidence_0279.json` retains the recovered raw Dodge builder:

`Fix(Level/5) + Fix((Charm-50)/5) + Fix((Agility-50)/3) + PlusDodge`, with the below-33-percent encumbrance bonus applied in that builder before the separately proven Dodge-vs-Accuracy resolution.

This proves a specific combat-stat ordering boundary. It does not establish a universal encumbrance stage for every character statistic.

### Combat status boundary

`DLLCombatResolutionOrder_0130.md`, sourced from `wccmmud.dll calculate_attack @0x0042b800`, proves that Armor Class participates in the hit-chance gate before the later Dodge gate. A negative `dodge_bn` (blind/debuff path) is not the screenshot's claimed universal "final Accuracy -50%" transformation. The screenshot's exact blindness formula is therefore not adopted.

The current incapacitation projection keeps Armor Class and damage resistance intact while suppressing active Dodge for stunned/paralyzed/sleeping states; that behavior remains **STRONGLY CORROBORATED**, not promoted to exact native PASS by this document.

### Periodic status timing

`spells.spell-round-three-seconds` is already PASS in the parity ledger. Decoded spell/status durations and periodic ticks use the recovered **3-second spell/system round**. Therefore the screenshot's proposed universal poison tick of "every 10 seconds" conflicts with the promoted Classic timing authority and is rejected.

## Screenshot-claim disposition

| Screenshot claim | Pass 36 disposition | Reason |
| --- | --- | --- |
| Class-base + per-level + race-modifier generic base-stat equation | **REJECTED for Classic creation** | Native creation evidence says starting stats are race minima. |
| Permanent stat abilities are flat signed deltas | **SUPPORTED / STRONG** | Abilities 46-51 explicitly increase/decrease the stat by parameter value. |
| All equipment applies flat modifiers then percentage modifiers | **UNPROVEN** | Retained native item projection proves additive direct combat fields, not a universal percentage stage. |
| Disease/curse/injury must enter before equipment percentages | **UNPROVEN** | No retained exact native call trace establishes that global ordering. |
| Encumbrance is a universal Step-3 modifier stage | **PARTIALLY SUPPORTED only for recovered combat formulas** | Raw Dodge and attack scheduling have explicit encumbrance placement; no global all-stat stage is proven. |
| Blindness applies a final -50% Accuracy override | **REJECTED as current Classic authority** | Native combat evidence uses the defender dodge/debuff field path; no universal -50% final Accuracy rule is recovered. |
| Sleep/paralysis force both Dodge and AC to zero | **UNPROVEN / NOT ADOPTED** | Current evidence supports Dodge/action suppression below PASS while preserving AC/DR; exact native hard-state struct mutation is still open. |
| Poison bypasses stats and ticks every 10 seconds | **PARTLY WRONG** | Poison is a periodic status family, but the promoted status clock is 3 seconds, not a universal 10 seconds. |
| One global linear modifier pipeline covers every statistic | **UNPROVEN** | Native evidence is formula-specific and uses cached/derived values; no single universal pipeline has been recovered. |

## Parity classification change

`character.cross-category-modifier-order` advances from **UNKNOWN** to **STRONG** because multiple category boundaries are now independently anchored by retained native evidence and the supplied document has been triaged against those anchors. It does **not** advance to PASS: the exact universal ordering across all race/class, ability, equipment, aura/status, and derived-stat contributions is still unresolved.

No gameplay formula is changed by Pass 36. The pass prevents unsupported screenshot rules from being copied into Classic runtime while making the verified partial ordering boundary explicit and regression-testable.
