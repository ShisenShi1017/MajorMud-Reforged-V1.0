import json, pathlib
r=pathlib.Path(__file__).resolve().parents[2]
p=r/"Reports/WorldgroupNative/worldgroup_textblock_special_command_0019.json"
d=json.loads(p.read_text())
assert d["confidence"]=="PROVEN"
assert d["function"]["start"]=="0x00471D55"
assert d["recordBuffer"]["size"]==2024
assert d["execution"]["maximumRecordFetches"]==50
assert d["execution"]["stopResult"]==1
assert (r/"Apps/Source/Reforged.Server/WorldgroupTextblockSpecialCommandRegistry.cs").exists()
print("Worldgroup textblock special-command evidence validated")
