# Engine Architecture Principles

Status: Draft architecture direction folded into CodeUpdate-0039.

## Direction

The Server should evolve as a reusable game engine capable of running multiple worlds and rule sets. It should not become a separate hardcoded server for Classic, Reforged, Custom worlds, or any single future mode.

Classic remains the first implementation target because it provides the verified baseline.

## Principles

- Favor reusable engine systems over feature-specific systems.
- Keep the engine generic whenever practical.
- Avoid naming or designing systems around Classic unless the behavior is truly Classic-specific.
- Let world behavior come from data whenever appropriate.
- Put engine behavior in reusable systems rather than hardcoded game logic.
- Future gameplay systems should plug into the engine instead of replacing it.
- Favor extensibility over shortcuts, but avoid premature complexity.
- Add extension points only when they provide clear future value.

## Design Questions

When designing new Server features, ask:

1. Can this be data-driven?
2. Can this be reused by future modes?
3. Can this be exposed to the Editor later?
4. Does this unnecessarily tie the engine to Classic?

## Long-Term Goal

Classic, Reforged, custom worlds, future expansions, and additional rule sets should not require separate server codebases.

## Boundary

This is planning direction only. CodeUpdate-0039 does not implement mode loading, Reforged mode, Custom mode, AI systems, alternate gameplay rules, or runtime gameplay systems.

