# CodeUpdate 0154 — Textblock Output Reference Semantics

This pass closes the runtime behavior that can be proven for the 519 decoded output-producing operations: 130 `message` references and 389 `text` references.

The runtime now represents both operations as typed canonical references. References preserve effect order, remain buffered inside the selected clause transaction, publish only after the clause succeeds, and are discarded on rollback. Numeric identifiers are never exposed as player-visible substitute text.

No payload grammar was invented. Message payload resolution, textblock payload resolution, substitution tokens, pronouns, pluralization, color controls, line formatting, pagination, and timing remain explicitly native-dependent until their source resources and dispatcher are reconstructed. The renderer therefore fails closed when asked to render an unresolved reference.

`RuntimeTextblockCommandAbilityCatalog` requires and validates `contract.textblock.output_reference` at startup. `RuntimeTextblockOutputBuffer` enforces canonical IDs, source ordering, single commit/rollback, and transactional output behavior.
