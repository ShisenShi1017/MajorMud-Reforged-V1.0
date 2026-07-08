# Runtime-0009 Combat Strategy

Runtime-0009 replaces the previous placeholder-only combat milestone with an executable combat foundation.

## What is implemented

- CombatantSnapshot model for players, monsters, and NPCs.
- CombatState model with structured event logging.
- Deterministic seeded duel execution.
- Initiative ordering scaffold.
- Attack resolution pipeline.
- Hit/miss event generation.
- Damage application and death events.
- Formula trace capture through the Formula Engine.

## Accuracy boundary

The combat module is executable, but it does **not** claim Classic MajorMUD formula accuracy.

Classic hit chance, damage, critical hits, dual wield, backstab, status interactions, spell-combat behavior, and exact speed timing remain UNKNOWN until evidence-backed.

## Runtime modes

The current executable path uses `reforged_formula` mode and `classic_accuracy = not_classic_claimed`.

Future formula modes should remain separated:

- classic_verified
- classic_hypothesis
- mmexplorer_derived
- reforged_formula

## Next work

The next milestone can safely build loot/economy/NPC services on top of death and combat-end hooks, or deepen combat with status effects and special attacks.
