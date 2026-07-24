import json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
p=root/'Reports/WorldgroupNative/worldgroup_special_command_interpreter_0020.json'
d=json.loads(p.read_text())
assert d['interpreter']['entry']=='0x00470209'
assert d['interpreter']['scratchReferenceCapacity']==100
assert d['recordFetch']['lockMode']==9
assert (root/'Apps/Source/Reforged.Server/WorldgroupSpecialCommandInterpreterRegistry.cs').exists()
print('worldgroup special command interpreter 0020: PASS')
