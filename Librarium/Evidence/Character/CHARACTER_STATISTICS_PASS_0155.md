# Character Statistics Pass 0155

Classification: **STRONGLY CORRELATED**

The authoritative Librarium ability catalog exposes direct numeric modifiers for cold, fire, stone, lightning, and water resistance; speed; HP regeneration rate; and mana regeneration percent. CodeUpdate0155 promotes only these explicit numeric mappings into the character statistic projection.

No base AC, attack, defense, initiative, saving-throw, weapon timing, or regeneration cadence formula is inferred. Those remain Unknown for native reconstruction.

## Provenance and runtime contract

- Source: `Data/Classic/abilities.json`
- Ability IDs: 3, 5, 65, 66, 87, 123, 145, 147
- Formula: `character.statistics.explicit_modifier_projection_0155`
- Assumption: the persisted character ability value is the numeric parameter described by the authoritative ability record.
- Fallback: missing ownership projects zero.
- Save impact: canonical derived-stat and resistance maps plus the projection marker are persisted.
- World-state impact: none beyond character-local combat/statistic state.
