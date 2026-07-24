# CodeUpdate 0151 — Command Parser and Dispatch Exhaustion

This pass reconstructs the complete active Reforged runtime command parser and dispatcher boundary from `RuntimeCommandSystem.cs`.

## Promoted behavior

- All 77 registered commands and 23 aliases are mapped to one of 19 runtime dispatch routes.
- Input is trimmed, split on spaces with empty entries removed, and only the command token is normalized to lower invariant case.
- Exact canonical names and aliases resolve before prefix matching.
- Prefix matches require each command's minimum abbreviation length; ambiguous results are returned in deterministic sorted order.
- Resolution errors return before command event publication or handler execution.
- Empty input renders the current room.
- Movement and nonmovement execution orders are represented separately, including movement guards, state persistence, broadcasts, and event publication.
- Argument consumption is classified exactly as ignored, joined remaining text, remaining token vector, or opaque `RuntimeCommand` forwarding.

## Runtime enforcement

`RuntimeTextblockCommandAbilityCatalog` now requires and validates `contract.command.parser_dispatch`. Startup rejects missing routes, unresolved routes, duplicate command names, unsupported argument modes, invalid parser normalization, or a non-fail-closed unknown-command policy.

## Generated artifacts

- `Librarium/BehaviorGraph/command_parser_dispatch_graph_0151.json`
- `Librarium/Reports/command_parser_dispatch_exhaustion_0151.json`
- `Data/Classic/textblock_command_ability_contracts.json`
- `Build/CodeUpdate0151/generate_command_parser_dispatch_semantics.py`
