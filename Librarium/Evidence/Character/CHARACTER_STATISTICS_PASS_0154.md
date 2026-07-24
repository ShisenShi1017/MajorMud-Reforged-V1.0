# Character Statistics Pass 0154

## Promoted runtime behavior

This pass establishes deterministic ownership and persistence for the explicit Character combat-statistic modifiers already exposed by the authoritative Librarium item projection.

Equipped item instances are evaluated in stable slot/instance order. Runtime performs checked additive aggregation of the four direct numeric item fields and replaces the corresponding canonical derived-stat keys on every recalculation:

- `armor_class_bonus`
- `damage_resistance_bonus`
- `accuracy_bonus`
- `speed_bonus`

The projection stores a formula identifier, timestamp, and ordered equipment-instance provenance marker. Save version 8 requires all four canonical keys and validates derived-stat, resistance, and saving-throw maps. Resistance and saving-throw identifiers are normalized deterministically without assigning invented formulas.

The source contains 1,950 items: 565 non-zero armor-class modifiers, 246 non-zero damage-resistance modifiers, 285 non-zero accuracy modifiers, and 393 non-zero speed modifiers.

## Classification

`STRONGLY CORRELATED`.

The numeric fields and equipped ownership are authoritative. Their direct additive aggregation is already established by equipment pass 0145. This pass promotes the Character-side projection, save ownership, ordering, and validation. It does not claim native base-stat formulas.

## Provenance and impact

- Source: `Data/Classic/items.json`
- Runtime: `RuntimeItemEngine.EquipmentModifierTotals`
- Character projection: `RuntimeCharacterCombatStatisticProjector`
- Formula ID: `character.statistics.explicit_modifier_projection_0154`
- Save impact: save version 7 persists canonical derived-stat maps and projection provenance
- World-state impact: none; recalculation reads authoritative equipped state and mutates only the owning Character record
- Fallback: missing or invalid canonical projection data fails validation; unresolved formula families remain non-executable

## Native-only unknowns retained

Base AC, attack, defense, initiative, weapon-speed conversion, resistance resolution, saving-throw resolution, regeneration, and cross-category modifier ordering remain unknown pending later Character/native reconstruction. No values or formulas were invented.
