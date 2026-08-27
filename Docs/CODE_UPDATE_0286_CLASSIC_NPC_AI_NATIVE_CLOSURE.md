# Code Update 0286 — Classic NPC Native AI / 16-bit Record Closure

## Scope

Pass 16 treats MajorMUD 1.11p as the legacy 16-bit target while preserving the actual native field widths recovered from Nightmare Redux. It does **not** force every value to `short`; the monster record mixes signed 16-bit integers, bytes, and signed 32-bit longs.

## Recovered native monster widths

Nightmare Redux `modFieldmaps.bas` defines `MonsterRecType` with these relevant storage classes:

- `Integer` (signed 16-bit): Group, DR, AC, Follow, MR, BS Defence, HP, Energy, HP Regen, Game Limit, Active, Type, Alignment, Regen Time, Charm Level/Resistance, attack accuracy/spell, attack min/max, attack energy, hit-spell ids.
- `Byte` (8-bit): Undead, attack type, attack percent, item-drop percent, mid-spell percent/level, gender.
- `Long` (signed 32-bit): monster Number, ExpMulti, WeaponNumber, Experience, message/text pointers, item ids, and currency quantities.

`RuntimeClassicNpcNativeFieldContract` validates decoded Classic records at that storage boundary. It does not emulate wraparound where no native arithmetic operation has been recovered.

### Follow is a percentage field

Nightmare Redux/MMUD Explorer export notes map native `Follow` directly to `Follow%`. The retained 1.11p corpus contains 1,100 NPCs and every decoded value is in `0..100` (24 distinct values). This proves the storage/field meaning, but **not** the exact native decision point at which Follow% is rolled; patrol/follow/track timing remains UNKNOWN.

### Attack percentages are bytes, not a constrained modern percent type

The native attack selection field is a byte. Retained executable rows include values above 100 (for example 200 and 232). Pass 10 already recovered the inclusive d100 threshold comparison, so values above 100 intentionally saturate to true when their slot is reached. Pass 16 therefore validates the byte width (`0..255`) and does not clamp to `0..100`.

## Native assist relationship

Ability 146 is decoded as `MonsGuards`. Its value is a monster legacy id. The verified relationship corpus projects 112 Classic NPC owners with at least one ability-146 target, and every projected target resolves to a Classic NPC definition.

Classic live combat now admits NPC guard assistance only when the candidate's decoded `guarded_npc_legacy_ids` contains the attacked NPC's legacy id. Generic Reforged `assist_target_instance_id` / `assist_group_id` relationships are isolated to non-Classic worlds.

This relationship is **STRONG**, not PASS, because the exact original timing, propagation radius, retry cadence, and priority relative to flee/aggro/target-selection remain native-unresolved.

## Message 8480 correction

Message 8480 is **not an assist broadcast**. The verified native payload is:

`%s %s %s with his %s, but %s dodges!`

It is a physical dodge/miss presentation template.

There are nine reachable Classic physical rows using message 8480:

- 4 rows on Thrag (`npc.59`) have exact `miss_verb3` and `weapon_name` values in retained public-reference evidence and are fully bound.
- 5 rows on Nahr (`npc.1006`) lack those caller-owned argument values. Native hit prose is insufficient to reconstruct them without inference, so these five rows remain UNKNOWN.

No value is guessed from the hit text.

## Residual native AI UNKNOWN

Pass 16 does **not** claim the image-proposed `Flee -> See -> Aggro -> Assist -> Target -> Spell/Melee -> Patrol -> Track` order as native. The remaining evidence gap is the original decision dispatcher itself: flee thresholds, base perception/aggro acquisition, target switching/threat ordering, patrol/pathing, tracking, and exact action priority/retry cadence.

## Parity accounting

Pass 16 adds two independently auditable requirements:

- `npc.native-monster-record-width-contract` — **PASS**
- `npc.monsguards-relationship` — **STRONG**

The two broad NPC UNKNOWNs remain, but are now narrower and accurately described.
