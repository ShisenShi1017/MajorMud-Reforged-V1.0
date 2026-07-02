# EngineKnowledge — Initial Draft

Status: initial working document. This is not runtime proof.

## Parser

Expected model: verb-first text parser with short aliases for directions. Direction aliases include n/s/e/w/ne/nw/se/sw/u/d and full names.

## Movement

Movement can be plain directional, typed/special, text-command based, key/item gated, lock/door gated, level gated, or quest/flag gated.

## Evidence classes

- original_runtime_confirmed: confirmed from original module/debugger/logs.
- mbbsemu_confirmed: confirmed from MBBSEmu research.
- data_confirmed: directly encoded in `/Data`.
- expected_model: simulator inference only.
- unknown: unresolved.

## Current confirmed project rules

- `/Data` is the runtime source of truth for recovered content.
- DataSimVer does not promote endpoints or special-exit semantics without evidence.
- Simulator evidence is labeled expected-model unless backed by runtime or reverse-engineered proof.


## DataSimVer0007 Addendum — Character State

Character state is now modeled as simulator input: level, class, race, inventory mode, quest/flag mode, and diagnostic privileges. This reflects the Worldgroup-era assumption that a compact engine interprets data tables and player state one command at a time.


## DataSimVer0008 Addendum
Service behavior is modeled as data-driven: rooms expose shops/trainers/spawns; shop inventory and regeneration are treated as interpreted fields; monster follow/charm/ability values are expected-model clues only until verified against runtime/MBBSEmu.


## DataSimVer0009 Finding — Conditional State and Quest/Flag Clues

`quests.json` currently contains zero quest records. Until original-engine evidence identifies the real flag tables or bit fields, quest behavior must be treated as inferred from surrounding data: room descriptions, item names/descriptions, NPC behavior fields, spell effects, and exit types.

DataSimVer0009 generated expected-model condition maps only. No `/Data` changes or promotions were made.


## DataSimVer0010 Addition — Player State Traversal

Virtual character profiles are now used to test expected behavior. Level, inventory, quest/progress flags, and spells are treated as inputs to the data-driven interpreter model. Results remain expected-model until confirmed by reverse-engineering.


## DataSimVer0014 — Inventory/Economy Findings
- Real measured 0014 pass executed from the uploaded DataSimVer0013 cumulative baseline.
- Items analyzed: 1950; shops analyzed: 178; resolved shop inventory refs: 1204.
- Persistent inventory/economy state can be simulated from item/shop data fields without modifying `/Data`.
- Expected-model transaction successes: 3357; failures: 7099.
- Inferred mechanics such as sell-back percentage and carry limit remain unpromoted until runtime/MBBSEmu evidence confirms them.
- `/Data` unchanged; room endpoints promoted: 0; special-exit semantics promoted: 0.


## DataSimVer0016 Cleanup/Search Visibility Hypothesis

User-provided runtime hypothesis: cleanup can hide some room/items instead of deleting them. Hidden items may require the `search` command to discover, and search success is affected by player stats. This is recorded as an engine-behavior hypothesis only; `/Data` remains unchanged. Future simulator work should distinguish visible, cleanup-hidden-searchable, and cleanup-deleted/removed item states.
