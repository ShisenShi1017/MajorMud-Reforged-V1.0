# Final Native Closure Update0025 — Support Payload Contracts

## Proven closures

- All three 4,000-byte BIN screens preserve DOS character/attribute cells and use only attribute bytes `7, 9, 11, 12, 14, 15`.
- `WCCADDON.SYS` is a nine-line table with grammar `<slot>:<uppercase token>` and contiguous slots 1–9.
- `WCCDMA.SYS` is a 152-line table with grammar `<IPv4> <8-digit identifier> <uppercase token>`.
- Every supplied DMA address and token is unique, while only 140 of 152 identifiers are unique; therefore the identifier is not a safe primary key.

## Runtime policy

Reforged preserves the original byte attributes and token strings verbatim. It parses only the proven grammars and fails closed on malformed records. It does not invent token generation, validation, or palette conversion behavior.
