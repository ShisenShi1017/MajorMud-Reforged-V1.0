# Validation0013 — Quest-Gate Single-Path Reverse Trace

## Objective
Validation0013 narrows the field-level validation effort to one concrete path instead of another broad scan.

Target path:

- The explicit MajorMUD message: `You will have to complete a quest to regain your Saint status.`
- The surrounding `_LOAD_PLAYER` logic where that message is emitted.
- The `_ASK_MONSTER_A_QUESTION` routine, because previous passes identified ASK/monster text handling as a likely quest interaction path.

## What Was Traced

### `_LOAD_PLAYER` quest-gate/status window
The disassembly window around the Saint-status quest message was captured in:

- `Evidence/ExternalSources/Validation0013/quest_gate_load_player_window.txt`
- `Evidence/ExternalSources/Validation0013/load_player_status_field_window.txt`

This trace shows a player-load routine checking and adjusting player-state offsets before printing the quest requirement message. The path is concrete and direct enough to promote the **existence of a load-time quest/status gate** to engine documentation.

### `_ASK_MONSTER_A_QUESTION` path
The ASK/monster text path was captured in:

- `Evidence/ExternalSources/Validation0013/ask_monster_question_window.txt`

The routine retrieves monster data, known-monster data, and a text block. This supports the working model that at least some interactive quest-like behavior is handled through monster text/question routines.

## Statistics

- Quest message hits: 2
- `_LOAD_PLAYER` lines bounded: 2276
- `_ASK_MONSTER_A_QUESTION` lines bounded: 473
- Candidate offsets captured in traced windows: 23
- `_ASK_MONSTER_A_QUESTION` `_GET_TEXT_BLOCK` calls: 1
- `_ASK_MONSTER_A_QUESTION` `_GET_KNOWN_MONSTER_DATA` calls: 1

## Findings

### Promoted to engine documentation

- MajorMUD has a load-time player status/quest gate that can emit the Saint-status quest requirement message.
- ASK/monster text handling is a concrete routine path for interactive monster/question text.

### Not promoted to `/Data`

No `/Data` promotions are approved. The trace identifies offsets and routines but does not yet prove stable, named source fields for the reconstructed data model.

Deferred:

- Exact player-state offset names.
- Exact quest persistence record/field mapping.
- Whether the Saint-status gate uses player-only state, quest flags, alignment/evil-point fields, or a mixed rule.
- Whether ASK/monster text handling directly mutates quest state or only displays/gates text.

## Promotion Review

Engine documentation promotion approved: **1**

`/Data` promotions approved: **0**

## Remaining Unknowns

- Exact quest record field names and offsets.
- Whether the quest completion state is stored in the player structure, Btrieve records, or both.
- How ASK/monster responses connect to persistent quest state changes.
- Cleanup flag encoding remains unresolved and was not the target of this pass.

## Next Pass

Validation0014 should continue the same narrow strategy. Recommended target:

- Trace `_ASK_MONSTER_A_QUESTION` beyond the initial text-block fetch and identify whether it writes player/quest state, or
- Switch to cleanup and trace `_CLEAR_SEARCH_FLAGS` plus the hidden-object state fields.
