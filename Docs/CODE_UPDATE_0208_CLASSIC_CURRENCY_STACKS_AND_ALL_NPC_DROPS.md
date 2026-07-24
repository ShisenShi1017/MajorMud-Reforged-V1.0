# Code Update 0208 — Classic Currency Stacks and All-NPC Drops

- Every defeated NPC produces its MMExplorer/generated wealth drop, with a minimum fallback when the source value is zero.
- Low-value wealth is rendered as copper farthings and silver nobles instead of forcing gold crowns.
- Currency of the same denomination stacks in the room.
- `g` is an exact alias for `get`; `g g`, `get gold`, `get silver`, and `get copper` collect matching currency stacks.
- Virtual currency is handled without requiring a generated item catalog record.
- Currency converts into the carried wealth scalar only when the player picks it up.
