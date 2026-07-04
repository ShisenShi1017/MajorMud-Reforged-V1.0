# ServerSim0036 Regression Candidates

These candidates are preserved for future reviewed gameplay/runtime work. They are not implemented by CodeUpdate-0062.

## Effective XP

Future tests should verify that awarded XP uses evidence-backed effective XP rather than raw base XP.

Candidate formula:

```text
effective_xp = override_xp ?? floor(base_xp * multiplier)
```

## XP Target Credit

Future tests should verify that XP credit can be scoped to the killed target's credited participant set rather than blindly splitting by full party count.

## Instant Regen Spawns

Future tests should verify instant regen only for currently identified stock cases:

- Cave worm room.
- Slime beast room.

All other spawn policies remain unpromoted until supported by stronger evidence.

## Combat-Off Re-Engage

Future tests should allow `*Combat Off*` to be followed immediately by `*Combat Engaged*` when another hostile relationship remains active.

## Stealth and Backstab

Future tests should distinguish:

- Attempting to sneak.
- Active sneaking.
- Sneak blocked.
- Failed or no-op sneak.
- Stealth broken on room entry.
- Backstab or surprise eligibility.
- Normal attack after stealth loss.

## Silver Rapier Blind Proc

Future tests should verify message timing, target status application, and proc chance only after stronger evidence exists.

