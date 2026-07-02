# DataSimVer0011 Summary — Action Resolution & Stateful Interaction Simulation

## Objective
Run the first full action-resolution pass using virtual player state. This pass tested whether the expected model can resolve movement plus common parser actions from the current room state without changing `/Data`.

## What Was Simulated
- Six virtual player profiles from baseline level 1 through diagnostic all-state.
- 1,246 rooms.
- Actions: move, open, unlock, enter, climb, crawl, push, pull, read, talk, buy, train, cast.
- Movement reused DataSimVer0010 traversal results.
- Non-movement actions used structured room/shop/spawn fields and text clues from `/Data`.

## Discoveries
- The simulator can now distinguish directional traversal from parser-style room actions.
- Doors/gates/locks and non-null exit types produce actionable open/unlock hypotheses, but key semantics remain unverified.
- ShopNumber bindings provide the strongest current evidence for `buy` behavior.
- Spawn and NPC/service text clues are useful for `talk`, but original response behavior remains unknown.
- Physical verbs such as push/pull/climb/crawl are detectable as targets, but success rules must wait for reverse-engineered engine evidence.

## Statistics
- Rooms analyzed: 1246
- Virtual profiles: 6
- Logged action attempts: 33300
- Expected-model successes: 20662
- Expected-model failures: 12638
- Requirement clue pairs: 69
- Failure reason categories: 11

## Changes
- Added `/Simulator/DataSimVer/0011/ActionResolutionMatrix`, `VirtualActionLog`, `StateTransitionGraph`, `RequirementInventory`, and `FailureReasonCatalog` outputs.
- Added validation and evidence outputs for DataSimVer0011.
- Updated documentation, changelog, manifest, EngineKnowledge, and Codex handoff.
- `/Data`: unchanged.

## Promotion Report
- Room endpoints promoted: 0
- Special-exit semantics promoted: 0
- Runtime behavior promoted into `/Data`: 0

## Remaining Unknowns
- Original engine parser token order.
- Exact key/item matching rules for locked exits.
- Whether open/unlock have separate state transitions or are aliases over movement checks.
- Physical action effects for push, pull, climb, crawl, and enter.
- NPC conversation/service response tables.
- Spell casting prerequisites and failure messages.

## Next Pass
DataSimVer0012 should focus on service-resolution and NPC interaction modeling: shops, trainers, banks, healers, taverns, and spawn-linked talk targets.

## Session Health
Healthy. This window is focused on DataSimVer beginning from DataSimVer0010. Context remains clear and no new window is needed yet.
