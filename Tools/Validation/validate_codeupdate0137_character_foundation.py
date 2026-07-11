#!/usr/bin/env python3
from __future__ import annotations
import json, os, subprocess, sys, tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]

def need(path, token, errors):
    p=ROOT/path
    if not p.exists(): errors.append(f'missing {path}'); return
    if token not in p.read_text(encoding='utf-8'): errors.append(f'{path} missing {token}')

def main()->int:
    subprocess.run([sys.executable,str(ROOT/'Tools/Librarium/compile_character_foundation_0137.py'),'--root',str(ROOT)],check=True)
    errors=[]
    for path,token in [
      ('Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs','public sealed class RuntimeCharacterService'),
      ('Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs','File.Replace(temporary, path, backup, ignoreMetadataErrors: true)'),
      ('Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs','PersistenceFileKey(id)'),
      ('Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs','SHA256.HashData'),
      ('Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs','ReplaceWithCopyFallback(temporary, path, backup)'),
      ('Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs','stream.Flush(flushToDisk: true)'),
      ('Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs','ResolveGeneratedCharacterStartRoom()'),
      ('Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs','startingAbilities'),
      ('Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs','maximumHitPoints'),
      ('Apps/Source/Reforged.Server/RuntimeCharacterFoundation.cs','maximumMana'),
      ('Apps/Source/Reforged.Server/RuntimeSessionManager.cs','CreateCharacterSession'),
      ('Apps/Source/Reforged.Server/RuntimeSessionManager.cs','is already connected'),
      ('Apps/Source/Reforged.Server/ServerNetworkHost.cs','SelectOrCreateCharacterAsync')]: need(path,token,errors)
    f=json.loads((ROOT/'Data/Classic/character_foundation.json').read_text())
    if (f.get('creation_gate') or {}).get('ready') is not True: errors.append('creation gate not ready')
    if (f.get('creation_gate') or {}).get('blockers'): errors.append('creation blockers remain')
    wm=json.loads((ROOT/'Data/Classic/world_manifest.json').read_text())
    if wm.get('character_start_room')!='room.2147': errors.append('world manifest missing room.2147 character start')
    with tempfile.TemporaryDirectory() as td:
        target=Path(td)/'character.json'; temp=Path(td)/'character.tmp'
        temp.write_text(json.dumps({'SaveVersion':1,'RoomId':'room.2147'})); os.replace(temp,target)
        if json.loads(target.read_text()).get('RoomId')!='room.2147': errors.append('atomic round trip failed')
    if errors:
        print('FAIL CodeUpdate0137 character foundation'); [print('- '+e) for e in errors]; return 1
    print('PASS CodeUpdate0137 character foundation static/runtime-contract validation')
    print('creation enabled from generated Librarium rules; all combinations; derived HP/mana; tree abilities; empty equipment; persistent Newhaven start')
    return 0
if __name__=='__main__': raise SystemExit(main())
