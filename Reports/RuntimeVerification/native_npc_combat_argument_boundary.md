# Native NPC Combat Argument Boundary — CodeUpdate0139

## Result

Message `8480` is a native physical dodge/miss template, **not an assist broadcast**. Its verified player payload is `%s %s %s with his %s, but %s dodges!`.

Native NPC combat presentation is exact for 735 of 736 reachable WCCMSG2 formats. The only partial format is message `8480`, used by five physical attacks on NPC `1006` (`Nahr`).

## Exhaustive Evidence Check

- Generated and Librarium attack records preserve all five `Nahr` attack rows and their exact hit, miss, and dodge message IDs.
- The retained OmniVerse WCC monster record for NPC `1006` omits both `miss_verb3` and `weapon_name` on every one of the five rows.
- Other NPCs using message `8480` prove that both values belong to the individual attack call site; they are not constants owned by the message.
- Other `Nahr` definitions use different weapons and different message families and therefore cannot safely supply the missing values.
- Native hit payloads identify some actions or anatomy, but two rows contain no body-part value at all. Reversing hit prose into caller arguments would invent text rather than recover decoded data.
- No original monster DAT/TXT source containing the omitted NPC `1006` fields exists in the current repository evidence surface.

## Runtime Boundary

The five rows retain safe fallback miss wording. No Classic value is inferred or hard-coded. Exact closure requires an original monster record, a native trace, or another immutable source that explicitly supplies the two caller-owned values for each attack.

## Subsequent Combat Closure

Decoded spell target type `12` now executes as one authoritative `area-hostile-target` cast. The selected NPC target owns execution, while every other persistent character in the same room receives an independent resistance, immunity, damage, status, persistence, and lethal death/corpse result. Per-session combat ticks cannot multiply the cast.

Decoded spell target type `11` is proven by original MMUD Explorer source history as `Full Area` and part of the native area-attack set. Its sole generated NPC use, NPC `361` casting spell `446` (`plague`), follows the same single-cast room fan-out for persistent characters and applies the decoded `4-10` untyped, never-resisted direct damage. Survivors also receive its exact 30-combat-round Dodge `-10`, AC `-5`, and Max Damage `-4` modifiers through the shared persisted status lifecycle.
