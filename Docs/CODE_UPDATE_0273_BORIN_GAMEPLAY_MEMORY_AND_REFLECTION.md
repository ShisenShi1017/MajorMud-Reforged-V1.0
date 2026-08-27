# CodeUpdate0273 — Borin Gameplay Memory and Reflection

## Purpose
Connect Borin's existing verified gameplay-learning and reflection systems directly to autonomous play so he can make decisions from his own experience instead of reasoning from the Librarium and the current terminal state alone.

## Changes

### Verified gameplay experience is now part of every autonomous decision
`BorinPlayerResearch.ProposeNextActionAsync` retrieves the world/character-scoped `BorinGameplayLearningSnapshot` before asking the brain for Borin's next action.

The decision contract now separates authority explicitly:

1. Current live server evidence controls the present moment.
2. Borin's verified personal gameplay history guides character-specific strategy.
3. Proven Librarium records guide game-world facts.
4. Strongly correlated/reference Librarium records remain hypotheses to verify during play.
5. Borin's own reflections are strategy notes and may be revised; they are never gameplay proof.

This lets Borin remember dangerous rooms, successful/failed commands, recovery behavior, routes, equipment observations, progression events, and other verified experience while still respecting the live server.

### Character-specific leveling experience
`BorinGameplayLearningStore` now retains verified positive experience changes by room while the character remains at the same level. Each retained leveling-area observation tracks:

- room
- number of verified XP-increase observations
- total captured XP increase
- largest captured XP increase
- character level range in which the observations occurred
- most recent observation time

These records are offered to Borin as historical character evidence when deciding where to level. They are explicitly not guarantees of future XP, safety, spawn state, or efficiency.

### Reflection is connected to verified experience and Librarium guidance
Automatic/manual player reflection now receives:

- recent meaningful live gameplay events
- Borin's verified gameplay-learning snapshot
- current Librarium guidance
- prior reflection entries
- conversation/social-learning context

The reflection contract requires Borin to revise an earlier strategy when stronger verified evidence contradicts it.

### Reflection continuity
The private reflection journal now exposes previous reflections as self-authored strategy hypotheses. They can maintain continuity between decisions and sessions without being promoted into evidence.

The journal continues to prune itself (250 reflections maximum), and gameplay-learning collections remain bounded, preventing unlimited raw-memory growth.

### High-value automatic reflection triggers
When Borin is safe enough to reflect and the reflection cooldown permits it, these high-value events may trigger a reflection without waiting for the normal meaningful-event count:

- combat defeat
- combat victory
- level gained

Normal periodic reflection still uses the configured event interval.

## Persistence
This update continues to use the existing local `AI/Memory` storage. Remote/private memory-repository synchronization is intentionally reserved for a later update after server-cleanup/session consolidation is implemented.

## Tests added
The deterministic Borin character harness now checks that:

- verified character XP gain is retained by leveling area and appears in Borin's gameplay-memory prompt
- leveling memory is explicitly bounded as historical evidence rather than a guarantee
- prior reflections are available for strategy continuity
- prior reflections remain explicitly labeled as non-evidence

## Verification note
The build environment used to prepare this cumulative update does not contain the .NET SDK, so a full `dotnet build` and Borin.CharacterTests execution must be run on the development machine before incorporation into main.
