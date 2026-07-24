# Code Update 0205 — Classic Shop, Equipment, and Encumbrance Fix

- Shop item matching now recognizes unique word-prefix abbreviations. Examples: `buy v` -> padded vest, `buy g`/`buy gl`/`buy gloves` -> padded gloves, and `buy helm` -> padded helm.
- Starter shop weapons and armor missing from equipment.json receive deterministic Classic runtime slot relationships from their authoritative item type/name evidence.
- Club and quarterstaff equip to WIELD; padded vest/pants/helm/gloves/boots equip to torso/legs/head/hands/feet.
- WIELD is displayed as `One handed` unless the item is inferred as two-handed.
- Encumbrance reads Strength case-insensitively and no longer falls back to capacity 1.
- Cumulative through Code Update 0204.
