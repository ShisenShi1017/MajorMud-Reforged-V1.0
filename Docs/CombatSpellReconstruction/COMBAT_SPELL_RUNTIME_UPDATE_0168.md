# Combat & Spells Runtime Update 0168

Implemented the executable boundary between the resistance/saving-throw catalog and direct spell mutation. Canonical request identity is checked before mutation. Immunity and negating saves suppress magnitude, vulnerability can amplify through signed basis points, absorption converts promoted direct damage into same-resource restoration, saves reduce magnitude or duration, and secondary effects can be suppressed. All target/caster mutations remain atomic through the existing direct-spell sink rollback. Production policies remain fail-closed.

## Native dependencies retained

- Complete production resistance and save mappings
- Exact absorption ownership and conversion rules
- Native duration rounding
- `spell.1304` resolution semantics
