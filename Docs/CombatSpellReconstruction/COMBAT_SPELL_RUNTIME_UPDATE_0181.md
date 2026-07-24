# Combat & Spells Runtime Update 0181

Implemented evidence-gated passive combat ability modifiers and transactional fear/charm control lifecycle cleanup. Passive policies contribute deterministic flat and basis-point changes without owning ability persistence. Control effects break only for promoted terminal reasons and downstream cleanup uses prepare/commit/reverse-rollback.

Production constants, native ordering, and persistence remain fail-closed pending stronger evidence.
