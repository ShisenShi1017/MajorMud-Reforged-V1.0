# Items Runtime Semantic Exhaustion — CodeUpdate0174

## Executable advancement

- Added evidence-gated death-protected item retention during character corpse creation.
- Added partial stack looting from corpses with source identity preservation, one transferred identity, owner-limit enforcement, destination-stack reconciliation, and atomic rollback.
- Preserved the CodeUpdate0173 nested portable-container hierarchy behavior.

## Evidence boundary

Classic native flag bits are not assigned. Protection activates only from explicit instance metadata (`death_protected` or `no_drop_on_death`). Partial looting activates only for item definitions already projected as stackable. The current Classic projection still has no proven stackable definitions, so Classic partial corpse loot remains fail-closed until later evidence promotes stackability.

## Remaining native dependencies

- Exact death-protection/no-drop flag mapping and exceptions.
- Exact corpse loot quantity command behavior and messages.
- Limited-item reservation treatment while corpse-held.
- Corpse ownership, permission, decay, restart, and resurrection semantics.
