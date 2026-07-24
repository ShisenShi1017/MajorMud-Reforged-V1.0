# Code Update 0190 — Classic Status Screen

Cumulative through Code Update 0190.

- Replaces the plain diagnostic `stat` output with a MajorMUD-style two-column status screen.
- `stat`, `stats`, `status`, and `score` all resolve to the same player status command.
- Displays name, race, class, level, experience, hits, armour class, six base attributes, lives/CP, and available Classic skills.
- Uses Classic green labels and cyan values.
- Removes internal world names, room IDs, and map IDs from the player-facing status screen.
- Preserves Update 0189 movement, room formatting, Arena spawn, gender, JSON, and host replacement fixes.
