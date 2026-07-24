# Combat & Spells Runtime Update 0159

## Executable runtime advancement

CodeUpdate0159 adds the first physical attack execution boundary. A physical attack must reference a scheduled turn issued by the owning combat instance, remain in the combat room, use active canonical participants, and supply a fully resolved evidence policy.

The executor consumes each scheduled turn once, uses the shared seeded deterministic random implementation, resolves hit before damage, snapshots the target before HP mutation, restores the snapshot and releases the turn if mutation fails, and emits the defender defeat transition only after a successful lethal commit.

## Evidence limits

The smoke profile uses fixed fixture values solely to prove execution order and rollback. It does not promote MajorMUD attack, defense, weapon, critical, armor, or resistance formulas. Production profiles with any unresolved numeric input fail closed.

## Persistence and ownership

Attack requests and traces are temporary action state. HP remains owned by Character/NPC sinks. Participant defeat and combat termination remain owned by `RuntimeCombatInstanceLifecycle`. No new temporary native state is persisted.

## Remaining native dependencies

Exact attack/defense formulas, weapon dice, strength and skill contributions, automatic hit/miss, critical/fumble, dodge/parry/block, armor/resistance ordering, ammunition, and durability remain unresolved.
