# Librarium-0003 — Graph Validation & Relationship Expansion

This pass continues from Librarium-0002 and strengthens the Librarium as the master knowledge graph.

## Purpose

- Expand first-class relationships from canonical data.
- Add room/exit, shop/item, loot/drop, monster/attack, room/shop, room/spawn, and ability classification relationships.
- Produce graph integrity and coverage reports.
- Produce quest flow candidates from ability references without treating sysop/editor commands as runtime proof.

## Results

- Nodes: 11791
- Relationships: 34845
- Ability records catalogued: 188
- Quest ability candidates: 13
- Missing edge sources: 0
- Missing edge targets: 0
- Duplicate edge keys: 0

## Important Rule

The Librarium is the source of truth. Runtime, server, editor, documentation, validation, and AI worlds consume this graph rather than creating isolated interpretations.

## Still Pending

NPC/textblock quest execution flow remains the next deep validation target. This pass maps available relationships and candidate quest flags, but it does not promote unproven quest stage ordering into `Data/quests.json`.
