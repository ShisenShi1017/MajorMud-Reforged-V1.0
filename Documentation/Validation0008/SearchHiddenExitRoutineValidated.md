# Search Hidden-Exit Routine — Validated Engine Documentation

Validation0008 validates the directional hidden-exit search path at routine level.

Verified behavior:
- `SEARCH <direction>` routes through `_CMD_SEARCH`.
- Parsed directions below 10 are passed to `_SEARCH_FOR_HIDDEN_EXITS`.
- Hidden-exit discovery uses room exit state/flags, a random roll, and a player-derived threshold.
- A successful discovery changes room/exit visibility state and calls `_ADD_EXIT_TO_LIST`.
- Failed checks produce directional “notice nothing different” output.
- Trap discovery uses a related branch but a separate player-derived value.

Not yet verified:
- The human-readable stat/skill names for player offsets `+5EDh` and `+5FBh`.
- Whether hidden items use the same formula.
- Whether DataSimVer `search.json` attributes exactly match the original compiled calculation.

Promotion status:
- Engine documentation: promoted.
- `/Data`: unchanged.
