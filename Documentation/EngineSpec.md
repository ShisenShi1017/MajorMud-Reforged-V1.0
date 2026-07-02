

## DataSimVer0012 Provisional Service Resolution
Expected model: a generic parser dispatches service verbs to shop/service tables, trainer records, and spawn records. This is not promoted as verified engine behavior.


## DataSimVer0014 — Inventory/Economy Findings
- Real measured 0014 pass executed from the uploaded DataSimVer0013 cumulative baseline.
- Items analyzed: 1950; shops analyzed: 178; resolved shop inventory refs: 1204.
- Persistent inventory/economy state can be simulated from item/shop data fields without modifying `/Data`.
- Expected-model transaction successes: 3357; failures: 7099.
- Inferred mechanics such as sell-back percentage and carry limit remain unpromoted until runtime/MBBSEmu evidence confirms them.
- `/Data` unchanged; room endpoints promoted: 0; special-exit semantics promoted: 0.


## DataSimVer0015 Expected Combat Model (Not Runtime-Proven)
Monster attacks expose min/max hit, accuracy, percent, energy cost, and optional hit_spell references. Spell rows expose target type, resource costs, min/max effect, duration, attack/resist types, difficulty, and ability tags. These fields support a table-driven combat/status interpreter model, but exact runtime formulas remain unverified.


## DataSimVer0016 Cleanup/Search Visibility Hypothesis

User-provided runtime hypothesis: cleanup can hide some room/items instead of deleting them. Hidden items may require the `search` command to discover, and search success is affected by player stats. This is recorded as an engine-behavior hypothesis only; `/Data` remains unchanged. Future simulator work should distinguish visible, cleanup-hidden-searchable, and cleanup-deleted/removed item states.
