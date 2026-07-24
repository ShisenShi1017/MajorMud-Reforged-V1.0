# Combat & Spells Runtime Update 0171

CodeUpdate0171 adds an evidence-gated combat status modifier boundary. Active spell effects are selected by canonical target ownership, ordered deterministically, resolved through promoted spell policies, scaled by magnitude and stack count, and accumulated with checked arithmetic.

The evaluator produces attack, defense, initiative, speed, resistance, and visibility values plus explicit physical-attack, spell-cast, and movement restriction decisions. It also emits a hidden-break signal without mutating Character-owned visibility state.

No production status meaning is inferred from an effect name. Missing, weak, or unresolved policies reject execution. Production status bindings, caps, multiplicative ordering, visibility/detection behavior, charm, and fear remain unresolved.
