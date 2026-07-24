#!/usr/bin/env python3
from pathlib import Path
import json, py_compile, sys
root=Path(__file__).resolve().parents[2]
required=[
 'Apps/Source/Reforged.Server/RuntimeCharacterSemanticState.cs',
 'Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs',
 'Data/NativeDecodeReports/CodeUpdate0140Character/character_record_source_inventory_0140.json',
 'Librarium/Evidence/Character/CHARACTER_RECORD_STRUCTURE_PASS_0140.md',
 'Librarium/Reports/character_semantic_exhaustion_status_0140.json']
for rel in required:
 p=root/rel
 if not p.is_file(): raise SystemExit(f'MISSING {rel}')
for rel in required:
 if rel.endswith('.json'): json.load(open(root/rel,encoding='utf-8'))
py_compile.compile(str(root/'Utilities/CharacterDecode/decode_character_record_sources_0140.py'),doraise=True)
cs=(root/'Apps/Source/Reforged.Server/RuntimeCharacterSemanticState.cs').read_text()
for token in ['CurrentSaveVersion = 3','EvidenceClassification','RuntimeCharacterSemanticState']:
 if token not in cs: raise SystemExit(f'C# contract token missing: {token}')
foundation=(root/'Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs').read_text()
for token in ['RuntimeCharacterRecordValidator.Validate','RuntimeCharacterRecordValidator.CurrentSaveVersion','RuntimeCharacterSemanticState.Empty']:
 if token not in foundation: raise SystemExit(f'Runtime reference missing: {token}')
print('CodeUpdate0140 Character validation: PASS')
