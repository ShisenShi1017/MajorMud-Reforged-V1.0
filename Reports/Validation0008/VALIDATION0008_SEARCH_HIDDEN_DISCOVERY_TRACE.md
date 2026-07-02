# Validation0008 — Search Formula and Hidden Discovery Targeted Trace

## Objective
Target the Search subsystem rather than broad-scanning all remaining blockers. The pass traces command search handling and hidden-exit discovery in the MajorMUD disassembly and compares it to prior DataSimVer search/hidden-object models.

## Evidence sources used
- Current Validation0007 cumulative baseline.
- Public MajorMUD disassembly: `MajorMUD/WCCMMUD.DLL.asm`.
- Prior reconstructed `/Data` skill model: `Data/Skills/search.json`.
- Prior simulator search expectations: `Simulator/DataSimVer/0017/SearchStatExpectedModel.csv`.

## What was validated
1. `_CMD_SEARCH` is a concrete exported command routine.
2. `_CMD_SEARCH` calls `_SEARCH_FOR_HIDDEN_EXITS` when parsing `SEARCH <direction>`.
3. `_CMD_SEARCH` applies a user action delay with `_ADD_DELAY`.
4. `_SEARCH_FOR_HIDDEN_EXITS` uses room exit state/flags, player-derived search values, and random rolls.
5. Hidden exits can be converted into active exits by changing room exit state and calling `_ADD_EXIT_TO_LIST`.
6. Trap discovery is a related but distinct search-path branch.

## Strongest implementation clues
- Hidden exit detection checks room exit type/state at offset `+360h` for the selected direction.
- A hidden-exit branch checks a flag byte at `+374h` with bit `2`.
- Discovery uses `GENRDN(0,100)` style random roll.
- The hidden-exit threshold uses a player value at `+5EDh`, adjusted by `-15`, with a minimum threshold of `3`.
- On success, the code sets room byte `+5EDh` to `1`, clears/changes direction fields around `+374h/+376h`, and calls `_ADD_EXIT_TO_LIST`.
- Trap detection uses exit state/type `9` and compares a random roll against a player value at `+5FBh`.

## Promotions
### Approved engine/documentation promotion
- Directional hidden-exit search has now moved from broad subsystem validation to routine-level validated behavior.
- Search is not just a generic skill model: `SEARCH <direction>` is implemented as a command path that calls `_SEARCH_FOR_HIDDEN_EXITS`.
- Hidden-exit reveal is stateful: successful discovery mutates room/exit visibility state and updates the player's exit list.

### Not promoted
- Exact player stat names behind offsets `+5EDh` and `+5FBh` remain unresolved.
- Exact hidden item formula remains unresolved; this pass strongly validates hidden exits, not every hidden object/item path.
- `/Data` remains unchanged because the exact data-field mapping for cleanup/hide/delete flags is still not proven.

## Statistics
- `_CMD_SEARCH` references: 23
- `_SEARCH_FOR_HIDDEN_EXITS` references: 50
- Random-roll calls inside hidden-exit routine: 2
- `_ADD_DELAY` calls inside `_CMD_SEARCH`: 2
- Evidence trace rows: 84

## Remaining unknowns
- Exact skill/stat mapping for player offset `+5EDh`.
- Exact trap-search stat mapping for player offset `+5FBh`.
- Whether general hidden item search uses the same threshold model or a separate routine.
- Whether the reconstructed `search.json` `int + agi` model matches the original player-field calculations.

## Recommendation for Validation0009
Target the player structure and derived stat calculations. Specifically map offsets `+5EDh` and `+5FBh` back to named player stats/skills so the search formula can be promoted with named attributes rather than raw offsets.
