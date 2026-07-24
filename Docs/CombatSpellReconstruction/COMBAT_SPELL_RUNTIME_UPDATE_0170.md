# Combat & Spells Runtime Update 0170

CodeUpdate0170 adds evidence-gated save-adjusted status duration and routes that duration through active-effect creation, refresh, replacement, and independent stacking.

## Runtime behavior

- `RuntimeStatusDurationPolicy` owns rounding and minimum duration.
- `RuntimeResolvedStatusApplication` rejects immunity, negation, secondary-effect prevention, weak policies, and missing duration policies.
- Magnitude resolution remains ordered before duration resolution.
- `RuntimeSpellEffectLifecycle.Apply` accepts a validated duration override while preserving the original API.
- Refresh retains the stable effect ID but recalculates expiration from the adjusted duration.
- Replacement terminates matching effects before creating the adjusted replacement.
- Independent effects each receive their own adjusted expiration.

## Safety

Production duration mappings and native rounding remain unresolved. Missing or weak policies fail closed. Active-effect persistence is unchanged and remains unpromoted.
