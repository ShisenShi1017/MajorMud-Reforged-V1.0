import json
from pathlib import Path
root=Path(__file__).resolve().parents[2]
d=json.loads((root/'Reports/WorldgroupNative/worldgroup_special_command_keywords_0021.json').read_text())
assert d['keywordCount']==41
assert d['keywords'][23]['keyword']=='text'
assert d['keywords'][24]['keyword']=='roomtext'
assert d['keywords'][31]['keyword']=='remoteaction'
assert d['remoteaction']['modeRangeInclusive']==[0,10]
assert d['remoteaction']['executor']=='0x0046C573'
assert (root/'Apps/Source/Reforged.Server/WorldgroupSpecialCommandKeywordRegistry.cs').exists()
print('worldgroup special command keywords 0021: PASS')
