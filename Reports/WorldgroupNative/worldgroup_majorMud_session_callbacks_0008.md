> **Correction:** Update0009 proves that `0x004789CA` is `WGSERVER!_injacr`, and that `0x0049D4D8`/`0x0049D48C` are imported `_globs`/`_nglobs` host globals. The loop is Worldgroup-owned, not MajorMUD-local.

# Worldgroup Native Closure Update0008 — MajorMUD Session Callback Classification

## Proven correction

The three WCCMMUD descriptor callbacks selected by Worldgroup are not three equivalent player-command handlers. Static control-flow recovery proves distinct roles:

- `0x004031D4` (state 4) initializes the MajorMUD session/player projection and always returns zero.
- `0x00405104` (default state) is the active command-dispatch gateway.
- `0x00408D3C` (state 5) performs transition cleanup and always returns zero.

## Active command path

`0x00405104` executes after the Worldgroup `_globalcmd` layer recovered in Update0007. It clears a session processing flag, then walks a MajorMUD-owned callback array at `0x0049D4D8` in FIFO order using the count byte at `0x00479665`.

The local callback results are exact:

- `-2`: run the per-session host action/cleanup path and return handled.
- `-3`: return handled.
- `-4`: continue to the next local callback.
- Any other result: enter the deeper fallback/parser continuation at `0x004789CA`.

This proves a two-tier interception model: Worldgroup global callbacks first, then MajorMUD-local callbacks, then the deeper MajorMUD parser.

## Remaining boundary

The parser continuation at `0x004789CA` is the next implementation target. Textblock and `remoteaction` execution remain downstream and are not inferred here.
