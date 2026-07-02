# DataSimVer0004 Summary — Gated Exit Classification
DataSimVer0004 classifies room movement constraints without changing `/Data` or promoting inferred behavior.
- Total rooms scanned: 1246
- Total exit rows scanned: 3022
- Missing/unresolved destinations: 285
- Room text gate clues: 847
- Room endpoint promotions: 0
- Special-exit semantics promoted: 0
## Direction coverage
- D: 43
- E: 633
- N: 692
- NE: 83
- NW: 66
- S: 701
- SE: 72
- SW: 82
- U: 28
- W: 622

## Classification counts
- possible_text_command_from_room_text: 1363
- possible_key_or_lock_from_room_text: 920
- direction_south: 701
- direction_north: 692
- direction_east: 633
- direction_west: 622
- typed_exit_unverified_semantics: 507
- possible_special_from_room_text: 338
- missing_or_unresolved_destination: 285
- direction_northeast: 83
- direction_southwest: 82
- direction_southeast: 72
- direction_northwest: 66
- possible_level_gate_from_room_text: 55
- direction_down: 43
- direction_up: 28

## Evidence policy
All command, key/item, lock/door, level, and special classifications are evidence labels only. They are not runtime proof and are not promoted into `/Data`.
