# MajorMUD Reforged — Validation Window Handoff

Current validation baseline: MajorMUD-Reforged-Validation0001-Cumulative.zip
Current DataSimVer baseline inside package: DataSimVer0024
Next valid validation pass: Validation0002

Rules:
- `/Data` remains source of truth and must not be changed without direct evidence.
- MBBSEmu evidence may validate host behavior.
- MajorMUD disassembly evidence may validate module behavior after routine mapping.
- MBBSEmu-compatible behavior is not automatically original MajorMUD behavior unless supported by module disassembly or direct runtime proof.

Next focus:
- Map MBBSEmu host lifecycle.
- Map `mcurou`, `finrou`, status dispatch, timer callbacks, and Btrieve APIs.
- Begin crosswalk between host callbacks and MajorMUD disassembly routines.
