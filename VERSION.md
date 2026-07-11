# MajorMUD Reforged Version

Current repository baseline: **CodeUpdate 0137 Internal - Character Foundation Catalog/Runtime Slice**

Status: CodeUpdate0135 and the internal CodeUpdate0136 Classic drop-roll repair are green by user report. CodeUpdate0137 internal Character Foundation local verification passed. GitHub Actions remains pending until the user pushes this internal 0137 commit and reports green.

Current internal 0137 scope: generated Character Foundation catalog and runtime plumbing. `Tools/Librarium/compile_character_foundation_0137.py` generates 13 playable races, 15 playable classes, 195 classified race/class pairs, and `Data/Classic/character_foundation.json` from Librarium SQL/MDB-backed records. Runtime adds account records, character records, atomic save/load, duplicate-login prevention, telnet account/character selection, and `--smoke-character-foundation`.

Boundary: CodeUpdate0137 does not complete Classic gameplay. Exact race/class compatibility, starting stats, HP/mana, skills, spells, equipment, alignment, and verified generated Classic character-start evidence remain blocked. Real character creation blocks rather than guessing a start room; `room.1` remains diagnostic-only. No partial public CodeUpdate0137 package should be created.

Local verification passed: `python Tools/Validation/validate_codeupdate0137_character_foundation_librarium.py`, `python Tools/Validation/validate_codeupdate0137_character_foundation.py`, `dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-restore`, `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --smoke-character-foundation`, and `python Tools/Validation/run_complete_runtime_verification.py`; complete runtime verification 53/53 PASS.

GitHub Actions remains pending until user push and green report.
