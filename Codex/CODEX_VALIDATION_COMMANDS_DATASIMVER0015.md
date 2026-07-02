# Validation Commands — DataSimVer0015

```bash
python -m json.tool Data/spells.json >/dev/null
python -m json.tool Data/monsters.json >/dev/null
python -m json.tool Validation/DataSimVer0015_CombatStatusValidation.json >/dev/null
wc -l Evidence/DataSimVer0015/MonsterCombatEvidenceExtract.csv
wc -l Evidence/DataSimVer0015/SpellStatusEvidenceExtract.csv
wc -l Simulator/DataSimVer0015/CombatEncounterSimulationLog.csv
wc -l Simulator/DataSimVer0015/SpellCastStateSimulationLog.csv
```
