# Combat & Spells Runtime Update 0160

Implemented evidence-gated physical weapon attack bindings and ordered defensive/mitigation processing. The runtime validates canonical definition bindings, weapon/ammunition readiness, scheduled-turn ownership, and active participants before resolving automatic hit/miss, attack-versus-defense, dodge, parry, block, critical damage, armor reduction, resistance, block reduction, and minimum damage. HP mutation remains atomic and lethal results transition through the combat-instance lifecycle.

No production MajorMUD constants were inferred. Missing thresholds, multipliers, reductions, ordering, or readiness semantics reject execution. `item.1622` is retained only as the existing strongly correlated canonical fixture binding.
