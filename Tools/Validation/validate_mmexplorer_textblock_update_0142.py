#!/usr/bin/env python3
import json, sys
from pathlib import Path
root = Path(sys.argv[1] if len(sys.argv) > 1 else '.')
out = root / 'Librarium/SourceImports/MMExplorerSQLData/TextblocksDecoded0142'
manifest = json.loads((out/'decode_manifest.json').read_text(encoding='utf-8'))
assert manifest['decoded_textblocks'] == 2279
assert manifest['decode_errors'] == 0
assert manifest['unknown_opcodes'] == []
assert manifest['unclassified_tokens'] == 0
assert manifest['source_directives'] == 0
assert manifest['npc_textblock_links'] == 771
assert manifest['quest_flows'] == 83
flows = json.loads((out/'quest_flows_decoded.json').read_text(encoding='utf-8'))
quest_records = json.loads((root/'Librarium/Objects/quests/quest_records_0123.json').read_text(encoding='utf-8'))
quest_ids = {record['id'] for record in quest_records['records']}
missing = [flow['id'] for flow in flows if flow['id'] not in quest_ids]
if missing:
    raise SystemExit(f"FAIL quest flows not promoted into Librarium: {missing[:10]}")
promoted = [record for record in quest_records['records'] if record.get('classification') == 'npc_textblock_quest_flow']
assert len(promoted) >= manifest['quest_flows']
assert quest_records.get('textblock_quest_flow_import_0142', {}).get('decoded_quest_flows') == 83
print(f"PASS: {manifest['decoded_textblocks']} textblocks, {manifest['npc_textblock_links']} NPC links, {manifest['quest_flows']} quest flows promoted to Librarium, 0 unknowns")
