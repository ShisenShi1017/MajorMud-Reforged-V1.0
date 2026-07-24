# Combat & Spells Runtime Update 0179

Implemented an evidence-gated runtime boundary for fear/charm control ownership and visibility. Fear emits a forced-movement request without selecting a destination. Charm emits explicit controller-target ownership without replacing NPC AI behavior. Control replacement, living state, room separation, and duplicate events fail closed.

Visibility aggregates promoted observer detection and target concealment effects in stable effect order. Hidden and invisible meanings are activated only through promoted policies. Targeting is allowed only when final detection meets or exceeds concealment. Production mappings, native thresholds, control break conditions, and persistence remain explicit native dependencies.
