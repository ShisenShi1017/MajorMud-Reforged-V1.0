# Combat & Spells Runtime Update 0169

Adds an evidence-gated periodic spell resolution coordinator. Periodic ticks now resolve immunity, vulnerability, resistance, absorption, saving throws, and secondary-effect suppression before atomic target mutation. Resolved status creation is rejected when the effect is immune, negated, or secondary effects are prevented.

Unresolved production mappings and native duration-rounding behavior remain fail-closed.
