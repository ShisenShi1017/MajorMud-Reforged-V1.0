import json,sys
from pathlib import Path
root=Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).resolve().parents[2]
p=root/"Data/NativeDecodeReports/CodeUpdate0147CharacterAbilities/character_ability_value_lifecycle_contract_0147.json"
d=json.load(open(p))
assert d["all_references_resolve"]
assert all(k in d["opcode_counts"] for k in ["giveability","addability","checkability","testability","failability"])
assert d["operation_occurrence_count"]==sum(d["opcode_counts"].values())
cs=(root/"Apps/Source/Reforged.Server/RuntimeCharacterAbilityValues.cs").read_text()
for token in ["checked(","ConditionPrefix","Give(","Add(","Check(","Test(","Fail(","Persist("] : assert token in cs
print("character abilities 0147 validation passed")
