# Combat & Spells Audit 0179

## Activated
- Fear and charm resolve to explicit temporary ownership decisions.
- Fear requests forced movement but does not choose a destination.
- Charm records controller and target but does not replace NPC AI behavior.
- Hidden/invisible concealment and detection are aggregated in deterministic effect order.
- Target observation and targeting require detection greater than or equal to concealment.

## Fail-closed
- Missing or weak policies.
- Unknown replacement rules and break conditions.
- Unknown production spell mappings and native thresholds.
- Unknown logout/restart persistence.
- `spell.1304` control or perception semantics.
