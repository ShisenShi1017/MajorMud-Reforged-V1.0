# CodeUpdate0131 Librarium Gap Audit + Server Smoke Test Prep

Status: **PASS**

## Summary
- World: `Classic`
- Data root: `Data\Classic`
- Families indexed: **35**
- Records indexed: **11201**
- Relationship references checked: **10374**
- Missing relationship references: **0**
- Warnings: **0**
- Errors: **0**

## Server smoke commands for Codex
```bash
dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj
dotnet run --project Apps/Source/Reforged.Server -- --smoke-generated-data
dotnet run --project Apps/Source/Reforged.Server -- --run-local
```

## Family readiness
| Family | Records | IDs | Evidence | Runtime mapping | Generator mapping | Pending/placeholder |
|---|---:|---:|---:|---:|---:|---:|
| ai_profiles | 27 | 27 | 27 | 27 | 27 | 27 |
| areas | 1 | 1 | 1 | 0 | 0 | 0 |
| assets | 1 | 1 | 1 | 0 | 0 | 0 |
| banks | 27 | 27 | 27 | 27 | 27 | 27 |
| classes | 58 | 58 | 58 | 58 | 58 | 58 |
| client_projection | 1 | 0 | 0 | 0 | 0 | 0 |
| client_views | 1 | 1 | 1 | 0 | 0 | 0 |
| commands | 25 | 25 | 25 | 25 | 25 | 25 |
| compile_manifest | 1 | 0 | 0 | 0 | 0 | 0 |
| conflicts | 1 | 1 | 1 | 0 | 0 | 0 |
| economy_rules | 22 | 22 | 22 | 22 | 22 | 22 |
| equipment | 1 | 1 | 1 | 0 | 0 | 0 |
| events | 1501 | 1501 | 1501 | 1501 | 1501 | 1501 |
| evidence_records | 1 | 1 | 1 | 0 | 0 | 0 |
| exits | 1 | 1 | 1 | 0 | 0 | 0 |
| formulas | 41 | 41 | 41 | 41 | 41 | 29 |
| guilds | 44 | 44 | 44 | 44 | 44 | 44 |
| items | 1950 | 1950 | 1950 | 1950 | 1950 | 4 |
| loot_rules | 1 | 1 | 1 | 0 | 0 | 0 |
| npcs | 1100 | 1100 | 1100 | 1100 | 1100 | 0 |
| quests | 2100 | 2100 | 2100 | 2100 | 2100 | 2100 |
| races | 2 | 2 | 2 | 2 | 2 | 2 |
| regions | 1 | 1 | 1 | 0 | 0 | 0 |
| rooms | 951 | 951 | 951 | 951 | 951 | 0 |
| scripts | 1 | 1 | 1 | 0 | 0 | 0 |
| shops | 178 | 178 | 178 | 178 | 178 | 0 |
| skills | 13 | 13 | 13 | 13 | 13 | 13 |
| spawns | 962 | 962 | 962 | 962 | 962 | 0 |
| spells | 1350 | 1350 | 1350 | 1350 | 1350 | 0 |
| status_effects | 111 | 111 | 111 | 111 | 111 | 111 |
| triggers | 719 | 719 | 719 | 719 | 719 | 719 |
| validation_rules | 1 | 1 | 1 | 0 | 0 | 0 |
| world_manifest | 1 | 0 | 0 | 0 | 0 | 0 |
| world_overlays | 2 | 2 | 0 | 0 | 0 | 0 |
| worlds | 4 | 4 | 0 | 3 | 3 | 3 |

## Relationship checks
```json
{
  "npc_formula_refs_checked": 4400,
  "npc_formula_refs_missing": 0,
  "room_spawn_refs_checked": 962,
  "room_spawn_refs_missing": 0,
  "spawn_room_refs_checked": 962,
  "spawn_room_refs_missing": 0,
  "spell_formula_refs_checked": 4050,
  "spell_formula_refs_missing": 0
}
```

## Interpretation
This audit does not prove full gameplay. It proves the generated Classic payload is structurally inspectable before the .NET server smoke test. Any warning here is a Librarium completion target; any error blocks server boot readiness.
