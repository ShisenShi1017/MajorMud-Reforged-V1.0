# Codex Handoff — DataSimVer0010

## Current Baseline
Use `MajorMUD-Reforged-DataSimVer0010-Cumulative.zip` as the latest cumulative verification package.

## Completed Verification Track
- DataSimVer0001: structural integrity.
- DataSimVer0002: world graph traversal.
- DataSimVer0003: standard exit validation.
- DataSimVer0004: gated-exit classification.
- DataSimVer0005: command parser behavior discovery.
- DataSimVer0007: virtual character profiles.
- DataSimVer0008: NPC/shop/trainer/service discovery.
- DataSimVer0009: quest/flag/condition discovery.
- DataSimVer0010: full virtual player-state traversal.

## Project Rules
- `/Data` is unchanged and remains the runtime source of truth.
- Simulator evidence is expected-model behavior, not original runtime proof.
- No old/nested ZIPs are included.
- No room endpoints or special-exit semantics were promoted.

## Current Understanding
Static data recovery is effectively complete. Behavior discovery is now based on a Worldgroup-era, data-driven interpreter model. The next major improvement should compare simulator assumptions against reverse-engineered original module/MBBSEmu behavior.

## Next Recommendation
Start a new conversation window before DataSimVer0011. Upload or use this ZIP as the baseline and provide reverse-engineering notes when available.
