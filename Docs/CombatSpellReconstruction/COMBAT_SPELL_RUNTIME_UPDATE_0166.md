# Combat & Spells Runtime Update 0166

Implemented evidence-gated direct spell damage, healing, drain, and resource restoration formulas. The runtime now supports deterministic fixed, random-range, level-scaled, attribute-scaled, and target-maximum percentage inputs; explicit rounding; resistance and save reductions; final and capacity clamping; atomic HP/mana/energy mutation; optional caster restoration for drains; and full rollback. Production coefficients remain inactive unless promoted. `spell.1304` remains fail-closed.
