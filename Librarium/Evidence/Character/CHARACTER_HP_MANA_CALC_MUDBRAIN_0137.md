# Character HP & Mana — pulled from wccmmud.dll

Max HP and Max Mana are (re)computed by **`calculate_secondary_stats @0x0041a424`**
every time stats change (creation, level-up, stat drain, equip). Creation calls it via
`ljndun @0x00403e76` → `calculate_secondary_stats(usrnum, 2)`. The HP-roll accumulator is
seeded by `roll_stats @0x0041a2d1` at creation and bumped by `train_level @0x0041e6e8`
each level. All offsets/logic below are from the Ghidra decompilation.

## Struct offsets used

| Offset | Field |
|--------|-------|
| player `+0x94` | level |
| player `+0x9c` | HEA (health stat) |
| player `+0xa2` / `+0xa4` / `+0xac` | INT / WIL / CHA |
| player `+0xae` | **max_hp** (result) |
| player `+0x600` | **max_mana** (result) |
| player `+0x724` | accumulated HP rolls (byte) |
| player `+0x468` / `+0x46c` | MaxHP / MaxMana ability cache (0 at creation) |
| class `+0x20` | HP per level |
| class `+0x22` | HP roll cap |
| class `+0x40` | magic_type (0-5) |
| class `+0x42` | magic_level |
| race `+0x2c` | race HP-per-level bonus |

## Max HP  (`calculate_secondary_stats`, writes to `+0xae`)

Verbatim from the DLL (lines that assign `+0xae`):

```
max_hp  =  HEA / 2                          (0x9c >> 1)
        +  level * class.hp_per_level        (0x94 * class+0x20)
        +  level * race.hp_bonus             (0x94 * race+0x2c)
        +  con_adj                           (added once)
        +  accumulated_hp_rolls              ((byte) player+0x724)
        +  con_adj                           (added AGAIN — the DLL adds it twice)
        +  MaxHP ability                     (player+0x468)
```

**con_adj** (the health-vs-50 term), exactly as the DLL computes it:
```
iVar = (HEA - 0x32) * level          ; (HEA - 50) * level
if (iVar < 0) iVar += 0xf            ; +15 floor-correction for negatives only
con_adj = iVar >> 4                  ; / 16   (arithmetic; truncates toward 0)
```
So a HEA of 50 contributes 0; every point of HEA above/below 50 adds/subtracts
`2 * level / 16` HP (it stays 0 until the product clears 16).

### The HP-roll accumulator (`player+0x724`)

- **At creation** — `roll_stats @0x41a2d1`: `player+0x724 = class+0x22` (the **full** roll
  cap, deterministic — a new character always gets the class's max HP roll).
- **Per level-up** — `train_level @0x41e6e8`:
  ```
  iVar6 = class+0x22 + 1
  cVar3 = genrdn(0, iVar6)            ; random 0 .. class.hp_roll_cap
  player+0x724 += cVar3
  ```
  So level-up HP is a random roll `0..cap`; only creation is the guaranteed full cap.

## Max Mana  (`calculate_secondary_stats`, writes to `+0x600`, switch on `class+0x40`)

```
magic_type 0 (non-caster):  max_mana = 0
magic_type 1..4 (casters):  max_mana = MaxMana_ability(0x45)
                                     + class.magic_level(0x42) * level * 2
                                     + player+0x46c            (equip cache, 0 at creation)
                                     + 6
magic_type 5 (Mystic):      max_mana = level - 1
```
Cases 1-4 are identical for mana; they differ only in the **spellcasting stat** they set
(`+0x604`): 1=(INT·3+WIL)/6, 2=(WIL·3+INT)/6, 3=(INT+WIL)/3, 4=(CHA·3+WIL)/6, 5=fixed 500.

### magic_type by class (from modern_classes)

| magic_type | classes | mana at level 1 |
|-----------|---------|-----------------|
| 0 | Warrior, Witchunter, Ninja, Thief | 0 |
| 1 | Gypsy, Warlock, Mage | magic_level·2 + 6 |
| 2 | Paladin, Cleric, Priest, Missionary | magic_level·2 + 6 |
| 3 | Druid, Ranger | magic_level·2 + 6 |
| 4 | Bard | magic_level·2 + 6 |
| 5 | Mystic | 0  (level−1) |

## Worked creation example (level 1, HEA 60, con_adj = 2×(10/16) = 0)

| Class | class_hp / roll_cap / mlvl | max_hp | max_mana |
|-------|---------------------------|--------|----------|
| Warrior | 6 / 4 / — | 30+6+0+0+4 = **40** | 0 |
| Mage    | 3 / 3 / 3 | 30+3+0+0+3 = **36** | 3·2+6 = **12** |
| Priest  | 3 / 3 / 3 | **36** | **12** |
| Mystic  | (magic_type 5) | **38** | 0 (level−1) |

Race HP bonus is 0/level for every race except Halfling (−1), so race barely affects
creation HP and only compounds at higher levels.

---
*mudbrain implementation: `calculate_secondary_stats()` in `sql/phase2_character_functions-BBS.sql`;
creation seeding in `create_character()` in `sql/phase9_creation-BBS.sql`. Verified matching
the DLL as of this doc.*
