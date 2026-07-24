# Combat & Spell Runtime Update 0154

Update 0154 adds the evidence-gated spell-target validation and deterministic target-set construction boundary required before a ready cast may execute effects.

## Executable advance

- `RuntimeSpellTargeting` accepts only canonical spell, room, and typed target identifiers.
- Target behavior is activated only by an evidence-bearing `RuntimeSpellTargetPolicy`; unresolved native target modes fail closed.
- Candidate snapshots are normalized in stable canonical-ID order and duplicate target IDs are rejected rather than silently merged.
- Self, explicit-single, party, room-creature, and no-explicit-target paths share the same kind, room, alive, visibility, friendly, and hostile gates.
- Maximum-target limits are applied only after filtering and deterministic ordering.
- Required modes reject empty target sets and emit an auditable decision containing accepted targets, rejection reasons, and evidence references.
- `spell.1304` retains its opaque target field and cannot execute through an invented target-mode mapping.

## Ownership

Combat/Spells owns target policy, target validation, target-set ordering, caps, and decision traces. Character, NPC, and World systems own authoritative candidate state: existence, room, life state, visibility, party membership, and relationship disposition.

## Validation

The pass validates mirrored Librarium/evidence JSON, canonical ID prefixes, unresolved `spell.1304` behavior, deterministic ordering, duplicate rejection, policy evidence/fallback requirements, C# structural references, Python syntax, generated-index agreement, cumulative preservation, manifest hashes, and ZIP integrity.

## Remaining dependency

Native target-mode constants, range, line of sight, hidden/invisible distinctions, corpse eligibility, friendly-fire defaults, random selection, directional and cross-room targeting, and object/world/remote target paths remain unpromoted for evidence or the final native pass.
