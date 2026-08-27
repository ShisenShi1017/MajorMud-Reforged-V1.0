# Pass 17 Validation 0287

## Result

Pass 17 focused static validation: **PASS (28/28)**.

Focused regression chain:

- Pass 10 NPC parity — PASS
- Pass 11 Rooms/Doors/Traps — PASS
- Pass 12 Economy/Transactions/Services — PASS
- Pass 13 Character progression closure — PASS
- Pass 14 Worldgroup ownership scope — PASS
- Pass 15 Death/Law/PvP/Lifecycle — PASS
- Pass 16 Native NPC AI — PASS
- Pass 17 Native room schema / trap state — PASS
- Combat runtime — PASS
- Combat runtime part 2 — PASS
- Native economy markup — PASS
- Gameplay Group 4 — PASS

## Parity correction

Pass 17 intentionally corrects a previous strict-parity overclaim. The MMUD Explorer exit projection preserved all 295 trap identities and their damage/spell operands, but dropped the native room-record `Para2` trap activation state. Because exact activation cannot be reconstructed from the exported display qualifier, Classic now fails closed rather than treating every decoded trap as active.

The effect consumers remain in place and preserve the decoded fixed-damage values, spell IDs, authoritative death routing, and authoritative spell engine. They can be re-enabled when per-exit native trap state and transition semantics are recovered.

## Compiler validation

The local analysis environment does not provide the .NET SDK, so a real `dotnet build -c Release` remains a required Windows-side compiler check.
