# Worldgroup Native Closure Update0009 — Command Import Boundary Correction

Static verification against the supplied `wccmmud.dll` proves that the previously named parser continuation at `0x004789CA` is an import thunk. Its instruction is `jmp dword ptr [0x0049D4E0]`, and the PE import table binds that IAT entry to `WGSERVER!_injacr`.

The callback vector and count used by the active WCCMMUD dispatcher are likewise host imports: `0x0049D4D8` is the IAT slot for `WGSERVER!_globs`, while `0x0049D48C` is the slot for `WGSERVER!_nglobs`. The loop at `0x0040514A–0x0040519B` therefore executes the Worldgroup-owned global command chain, not a MajorMUD-local callback array.

## Proven result controls

- `-2`: execute the per-session host action at `0x0046B170`, then finish as handled.
- `-3`: finish as handled.
- `-4`: continue to the next host-global callback.
- Any other result: invoke `WGSERVER!_injacr`, then finish as handled.

When the chain exhausts, WCCMMUD sets session flag `0x2000` and enters its own session-state jump table. That jump table is now the correct next parser boundary.
