# Native NPC Combat Argument Boundary — CodeUpdate0139

## Result

Message `8480` is a native physical dodge/miss template, **not an assist broadcast**. Its verified player payload is `%s %s %s with his %s, but %s dodges!`.

Native NPC combat presentation is exact for all 736 reachable WCCMSG2 formats. Message `8480`, used by five physical attacks on NPC `1006` (`Nahr`), is exact with two intentionally empty caller arguments.

## Exhaustive Evidence Check

- Generated and Librarium attack records preserve all five `Nahr` attack rows and their exact hit, miss, and dodge message IDs.
- The retained OmniVerse WCC record omits `miss_verb3` and `weapon_name`, but the original WCCMMUD 1.11p DLL plus `wccknms2.dat` execution trace proves this omission represents native empty strings rather than lost prose.
- Nahr's relevant native equipped-item field is `0`; `_generate_monster` copies it into active state, `_move_monster_to_fighter` NULs both buffers, and `_attack_monster_user` passes those buffers unchanged to message 8480.
- Thrag's equivalent field contains item `209` (`huge darkwood club`), while Nahr variants `1004`, `1005`, and `1011` contain item `1023` (`nexus blade`). NPC `1006` uniquely follows the zero-item branch.
- No hit phrase, anatomy, fist, foot, or normalized verb is inferred.

## Runtime Boundary

The five rows carry `native_empty_miss_arguments_verified=true`. Runtime renders the retained template with empty second and fourth string arguments, producing native spacing such as `Nahr  Shisenshi with his , but Shisenshi dodges!`. Other unresolved blank arguments still fail closed.

## Subsequent Combat Closure

Decoded spell target type `12` now executes as one authoritative `area-hostile-target` cast. The selected NPC target owns execution, while every other persistent character in the same room receives an independent resistance, immunity, damage, status, persistence, and lethal death/corpse result. Per-session combat ticks cannot multiply the cast.

Decoded spell target type `11` is proven by original MMUD Explorer source history as `Full Area` and part of the native area-attack set. Its sole generated NPC use, NPC `361` casting spell `446` (`plague`), follows the same single-cast room fan-out for persistent characters and applies the decoded `4-10` untyped, never-resisted direct damage. Survivors also receive its exact 30-combat-round Dodge `-10`, AC `-5`, and Max Damage `-4` modifiers through the shared persisted status lifecycle.
