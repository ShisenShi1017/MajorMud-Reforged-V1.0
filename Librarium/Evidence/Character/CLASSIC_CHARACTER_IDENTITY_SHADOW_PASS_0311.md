# Pass 40 — Classic character identity / Shadow LOOK boundary

Pass 40 verifies the Black Embroidered Robes disguise question without treating the supplied identity page as native authority.

## Recovered / retained evidence

- Native identity evidence already proves first-name uniqueness and UserRec name storage widths: 10-byte Given/FirstName and 18-byte Family/LastName.
- Retained ability `9` is named **Shadow** and its reference description is: `Displays "A shadowy figure!" when looked at by other players.`
- `item.1331`, **black embroidered robes**, carries `Shadow = 100`; the retained item/ability relationship is marked Verified.
- A historical MajorMUD item table independently lists Black Embroidered Robes with `Shadow +100`.

## Runtime projection

Classic `LOOK <character>` now checks equipped item abilities. A positive equipped Shadow ability masks the target identity for another player and emits `A shadowy figure!`.

The implementation is ability-driven rather than item-name-driven. Self LOOK is not masked.

## Important room-list boundary

Pass 40 does **not** replace the character name in the room occupant list. The retained Shadow description is specifically scoped to being **looked at by other players**. There is no recovered native evidence proving that ordinary room population output is also masked, so Reforged leaves that path unchanged instead of inventing a broader disguise rule.

## Still unresolved

Minimum/maximum playable name length beyond the recovered storage widths, allowed characters, reserved-name list, capitalization behavior, exact validation messages, title precedence, hidden/invisible/disguise precedence, and native room-list Shadow rendering remain gated.
