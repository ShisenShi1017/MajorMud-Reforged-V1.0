# DataSimVer0010 Player-State Model

This pass formalizes virtual player traversal as the main simulator strategy.

The model is intentionally Worldgroup-era and data-driven:

1. Load one room and one player state.
2. Parse the attempted action/direction.
3. Evaluate visible data clues: direction, exit_type, room text, key/item words, level words, quest/flag words, spell words, and command words.
4. Record the expected result.
5. Write evidence immediately and avoid mutating `/Data`.

The model is conservative. It does not claim to prove original runtime behavior. It prepares a comparison matrix for reverse-engineered engine findings and MBBSEmu behavior.
