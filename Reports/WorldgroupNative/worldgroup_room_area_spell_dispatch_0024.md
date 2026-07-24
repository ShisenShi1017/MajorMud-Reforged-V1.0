# Worldgroup Native Closure Update0024 — Room/Area Spell Dispatcher

- Function: `wccmmud.dll!0x00449232-0x00449BF0` (2,494 bytes, cdecl, four arguments).
- Accepts definition types 6 and 7.
- Resolves caster, class/archetype, and room/area records before execution.
- Enforces resource (`+0xBA/+0xBC`), secondary-resource (`+0x602/+0xF0`), and level (`+0x94/+0xBE`) requirements.
- Uses a bounded success roll and processes ten effect slots.
- Successful execution pays full costs; failed-roll execution pays full primary cost and half the signed secondary requirement.
- Effect 143 reaches room mutation helper `0x0046C241`; selected effects reach generic helper `0x0043FC13`.
- Caster output flushes through `0x0046B170`; room broadcast uses `0x0046B010`.
- Numeric effect semantics are intentionally not invented.
