# Remaining Systems Update0011 — PvP Policy

Implemented a fail-closed PvP policy coordinator that validates room policy, mutual consent, level range, party and guild restrictions, attack cooldowns, and logout restrictions before Combat execution.

The service emits typed consequence requests for kill credit, Law evaluation, and loot policy. Combat, Character, Economy, Party, Organizations, Rooms, and Law retain their authoritative state and formulas.

## Native dependencies

Exact MajorMUD 1.11p consent rules, zone encodings, level/alignment restrictions, criminal and Evil Point behavior, kill credit, death penalties, loot/corpse access, guards, bounty integration, cooldowns, logout restrictions, commands, and rendered messages remain assigned to the final native pass.
