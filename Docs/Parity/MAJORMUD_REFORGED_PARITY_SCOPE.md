# MajorMUD Reforged Parity Scope

MajorMUD Reforged targets **MajorMUD gameplay and player-visible behavior**, including behavior required by legacy clients such as MegaMUD. It does not target binary or architectural equivalence with the Worldgroup host.

## Required

- Commands, prompts, output text, state transitions, timing, formulas, persistence, room/world behavior, combat, economy, traps, character lifecycle, organizations, and other observable MajorMUD semantics.
- Host-derived behavior when MajorMUD relied on it and the result is observable to a MajorMUD player or client.
- Telnet/game-stream behavior required by MegaMUD.

## Not a parity requirement

- Worldgroup-internal PAGE/telegram transport or handoff APIs.
- Worldgroup mail/telegram storage internals.
- Worldgroup presentation queues and internal host priority routing.
- Worldgroup-specific logging internals.
- Binary/API/layout equivalence that has no MajorMUD gameplay or client-visible effect.

A Worldgroup detail may still be used as evidence to recover MajorMUD behavior. It is only excluded as an implementation requirement when no player/client-visible semantic depends on reproducing that host mechanism.
