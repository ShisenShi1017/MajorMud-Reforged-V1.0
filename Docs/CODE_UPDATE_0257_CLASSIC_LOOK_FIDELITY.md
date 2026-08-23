# Code Update 0257 — Classic Character LOOK Fidelity

## Scope

This incremental update corrects normal `LOOK <character>` rendering and relocates decoded race lore to the Classic race-selection help flow.

## Runtime changes

- Normal character LOOK no longer prints the selected race's long lore paragraph.
- Race lore remains authoritative and is shown from the race-selection menu with `? <number>` (for example, `? 2`).
- Character appearance now follows the native MajorMUD sentence order:
  1. health/build, race/class, hair and eye details;
  2. agility and charm;
  3. intellect and willpower;
  4. wound condition.
- The bracketed heading retains the full character display name while the prose uses the saved given name.
- LOOK reads the selected/current full stat keys before compact racial-minimum compatibility keys.
- Active Classic spell/status stat modifiers are included for strength, agility, intellect, willpower/wisdom and charm.
- Equipment output remains after the appearance paragraph and is not blocked by race-description lookup.
- Existing unique leading-name abbreviations remain supported.

## Example

`[ Shisenshi Sysop ]`

`Shisenshi is a healthy, well built Dwarf Witchunter with short brown hair and brown eyes. He moves gracefully, and is quite unfriendly and aloof. Shisenshi appears to be quite stupid and looks fairly knowledgeable. He is unwounded.`

The exact adjectives depend on the character's saved current stats and active status modifiers.
