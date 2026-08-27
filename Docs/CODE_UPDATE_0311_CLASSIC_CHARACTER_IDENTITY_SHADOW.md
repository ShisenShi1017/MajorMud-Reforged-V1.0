# Code Update 0311 — Pass 40 Classic Character Identity / Shadow LOOK

Pass 40 connects the retained Shadow ability presentation contract to live Classic LOOK behavior.

- Ability 9 `Shadow` retains the reference description `Displays "A shadowy figure!" when looked at by other players.`
- Black Embroidered Robes (`item.1331`) carry Shadow +100.
- Other positive equipped Shadow items use the same ability-owned behavior; item 1331 is not hardcoded as the only trigger.
- `LOOK <character>` by another player masks the heading to `A shadowy figure` and emits `A shadowy figure!`.
- Self LOOK is not masked.
- Normal room occupant rendering intentionally continues to use the character name because the retained evidence is LOOK-scoped; native room-list Shadow behavior remains unresolved.
- The user-provided 3-15 letters-only/reserved-name/title/invisibility page remains hypothesis-only and is not promoted.

Parity change: `native.character-identity-policy` UNKNOWN -> STRONG.

Current score: **97 PASS / 81 STRONG / 22 UNKNOWN = 200**. Strict parity **48.5%**; evidence-backed **89.0%**.
