# Worldgroup Implicit CAST/INVOKE Dispatch — Update0014

Direct disassembly closes the successful entity-match branch at `wccmmud.dll!0x0045083B-0x0045090D` as an implicit spell/power target shortcut.

After the bounded entity matcher resolves the current input to an entity, the branch reads the 16-bit record id at player offset `+0x92` and resolves it through `0x00432BD3`. The resolved record word at `+0x40` chooses the action selector passed to the shared executor `0x00461273`:

- Type `5` keeps selector `0x6C`, which the executor proves is **INVOKE** and KAI-specific.
- Every other type converts the selector to `0x1F`, which the executor proves is **CAST**.

The executor's embedded diagnostics independently confirm the classification: the `0x6C` branch uses KAI, power, and INVOKE language, while the `0x1F` branch uses spell and CAST language. A nonzero executor result marks the input handled; zero falls through to later gameplay command processing.

This proves player field `+0x92` is the class/archetype record reference used for CAST-versus-INVOKE selection, and record type `5` is the KAI category. It does not yet close the complete spell/power record layout or the full internals of `0x00461273`.
