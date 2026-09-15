# MajorMUD ANSI parity reference — capture 9046

This document records visual facts observed from the supplied MegaMUD/MajorMUD reference recording. The recording is treated as a visual parity source only; it is not committed to the repository. Raw Telnet bytes are still required before any escape-sequence claim is promoted to byte-exact parity.

## Confirmed visual roles

### Combat

- Player successful melee/critical damage lines render bright red.
- Player miss/dodge-result lines render bright cyan.
- Incoming NPC attack prose renders bright cyan.
- `*Combat Engaged*` and `*Combat Off*` render yellow/gold.
- NPC death prose, ground-drop prose, and experience award prose render light gray/white in the observed sequence.
- Ordering at combat completion is death/drop/experience, then `*Combat Off*`, then the normal prompt.
- The normal prompt appears without requiring an extra Enter key.

### Room movement / room display

- Room title renders bright cyan.
- Room description renders light gray/white and wraps naturally to the terminal width.
- The description is indented from the left margin in the observed movement display.
- `You notice ... here.` renders bright cyan.
- `Also here:` renders magenta; occupant names render bright cyan.
- `Obvious exits:` and exit text render bright green.
- The normal `[HP=...]` prompt follows the room block without an extra blank line.

### Inventory

- Carried-item/equipment listing renders light gray/white.
- Key listing renders light gray/white.
- `Wealth:` uses green for the label and cyan for the value/content.
- `Encumbrance:` uses green for the label and cyan for the numeric weight/capacity portion.
- The qualitative encumbrance band/percentage is visually distinguished in yellow/gold in the observed Medium example.
- Long inventory lines wrap at terminal width rather than being horizontally scrolled.

### Status / stats

- STATUS is a fixed-width three-column layout.
- Labels render green.
- Values render cyan.
- The first rows contain Name/Lives-CP, Race/Exp/Perception, Class/Level/Stealth, and Hits/Armour Class/Thievery.
- Lower rows preserve aligned Strength/Agility, Intellect/Health, Willpower/Charm, and right-column skills.
- RealmGate's existing 18 / 21 / 18 character STATUS grid matches the observed structure closely and should remain the layout baseline.

## Terminal behavior to preserve

- Black terminal background.
- Fixed-width/monospace text.
- 80-column-compatible wrapping behavior.
- CR/LF completion before prompt redraw.
- Partial prompts must remain visible without requiring CR/LF from the player.
- No duplicate prompts or extra blank lines after asynchronous combat completion.

## Regression policy

Visual facts above may be encoded as server-output and terminal-state regressions. Do not infer an exact SGR byte sequence from the recording alone. Exact SGR/CSI bytes, cursor movement, erase commands, and timing-sensitive terminal behavior require raw Telnet capture evidence before being marked byte-exact.
