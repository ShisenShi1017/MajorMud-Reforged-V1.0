# DataSimVer0005 Summary — Worldgroup Command Parser Behavior Discovery

DataSimVer0005 treats MajorMUD as a Worldgroup-era, text-command, data-interpreter game. It does not alter `/Data` and does not promote inferred behaviors.

## Results

- Total rooms scanned: 1246
- Total items scanned for name correlation: 1950
- Parser token rows: 3042
- Command clue rows: 640
- Room/object clue rows: 994
- Exit type parameter rows inventoried: 3022
- Unique exit type values observed: 21
- Room endpoint promotions: 0
- Special-exit semantics promoted: 0

## Top command verbs harvested

- look: 139
- run: 104
- open: 53
- walk: 47
- enter: 39
- move: 34
- read: 30
- use: 28
- cross: 27
- turn: 23
- go: 22
- press: 22
- say: 15
- lock: 10
- drink: 8
- board: 8
- climb: 7
- push: 6
- ring: 5
- ascend: 4

## Top object clues harvested

- wall: 211
- door: 85
- tunnel: 84
- well: 66
- sign: 53
- path: 46
- passage: 38
- gate: 32
- statue: 29
- pool: 24
- window: 19
- boat: 17
- river: 11
- tree: 10
- curtain: 9
- chest: 6
- rope: 6
- opening: 4
- rune: 4
- cave: 4

## Interpretation

This pass is meant to make later simulations smaller and less likely to freeze. Instead of brute-forcing every possible action in every room, it builds a candidate command list from room text, exit fields, item-name correlations, and raw exit parameters.

## Evidence policy

All results are expected-model behavior clues only. They are not original runtime proof. Reverse-engineered findings from the original module or MBBSEmu can later be compared against these inventories and used to refine the simulator rules.
