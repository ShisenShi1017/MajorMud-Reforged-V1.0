#!/usr/bin/env python3
"""CodeUpdate0130 no-SDK validation.

Validates the pieces needed for tomorrow's Codex/.NET server boot test:
- DLL-derived combat evidence is retained in Evidence and Librarium.
- The three combat conflicts are closed in Librarium and generated Data.
- Data/Classic has required generated runtime families.
- Config/server.worlds.json points Classic at generated Data/Classic and valid start room.
- Server source has generated-data smoke command and catalog loader code.
"""
from __future__ import annotations
import hashlib, json, pathlib, sys
ROOT = pathlib.Path(__file__).resolve().parents[2]
REQ_FAMILIES = [
    'world_manifest','compile_manifest','rooms','exits','spawns','npcs','items','spells','formulas','shops','quests'
]

def load_json(path: pathlib.Path):
    with path.open('r', encoding='utf-8') as f:
        return json.load(f)

def count_records(path: pathlib.Path) -> int:
    data = load_json(path)
    if isinstance(data, list): return len(data)
    if isinstance(data, dict):
        if isinstance(data.get('record_count'), int): return data['record_count']
        if isinstance(data.get('records'), list): return len(data['records'])
        return 1
    return -1

def sha(path: pathlib.Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main() -> int:
    errors: list[str] = []
    evidence = ROOT/'Evidence/Combat/DLLCombatResolutionOrder_0130.md'
    lib_evidence = ROOT/'Librarium/Evidence/Combat/DLLCombatResolutionOrder_0130.md'
    for p in [evidence, lib_evidence, ROOT/'Librarium/Evidence/Combat/dll_calculate_attack_evidence_0130.json']:
        if not p.exists(): errors.append(f'missing evidence file: {p.relative_to(ROOT)}')
    if evidence.exists() and 'calculate_attack @0x0042b800' not in evidence.read_text(encoding='utf-8'):
        errors.append('DLL combat evidence missing calculate_attack address marker')

    for rel in [
        'Librarium/Objects/formulas/combat_damage_reduction_physical.json',
        'Librarium/Objects/formulas/combat_dodge_order.json',
        'Librarium/Objects/formulas/combat_special_defense_order.json',
        'Librarium/Objects/formulas/combat_hit_vs_ac.json',
    ]:
        p = ROOT/rel
        if not p.exists():
            errors.append(f'missing formula record: {rel}')
            continue
        data = load_json(p)
        if data.get('conflict_status') != 'resolved':
            errors.append(f'formula conflict not resolved: {rel}')
        if data.get('resolved_in_code_update') != '0130':
            errors.append(f'formula not marked 0130: {rel}')

    conflicts = load_json(ROOT/'Data/Classic/conflicts.json')
    for rec in conflicts.get('records', []):
        if rec.get('open_conflicts'):
            errors.append('generated Data/Classic still contains open conflicts')

    data_root = ROOT/'Data/Classic'
    family_counts = {}
    for fam in REQ_FAMILIES:
        p = data_root/f'{fam}.json'
        if not p.exists():
            errors.append(f'missing Data/Classic family: {fam}.json')
        else:
            family_counts[fam] = count_records(p)
            if family_counts[fam] < 0:
                errors.append(f'invalid record count for {fam}.json')

    cfg = load_json(ROOT/'Config/server.worlds.json')
    classic = next((w for w in cfg.get('worlds', []) if w.get('id') == 'classic'), None)
    if not classic:
        errors.append('Config/server.worlds.json missing classic world')
    else:
        if classic.get('dataRoot') != 'Data/Classic': errors.append('classic dataRoot is not Data/Classic')
        rooms = load_json(data_root/'rooms.json').get('records', []) if (data_root/'rooms.json').exists() else []
        room_ids = {r.get('id') for r in rooms}
        if classic.get('startRoom') not in room_ids:
            errors.append(f'classic startRoom {classic.get("startRoom")} not found in Data/Classic/rooms.json')

    program = (ROOT/'Apps/Source/Reforged.Server/Program.cs').read_text(encoding='utf-8')
    catalog = (ROOT/'Apps/Source/Reforged.Server/GeneratedWorldDataCatalog.cs').read_text(encoding='utf-8') if (ROOT/'Apps/Source/Reforged.Server/GeneratedWorldDataCatalog.cs').exists() else ''
    if '--smoke-generated-data' not in program:
        errors.append('server Program.cs missing --smoke-generated-data path')
    if 'GeneratedWorldDataCatalog' not in catalog:
        errors.append('missing generated world data catalog source')

    report = {
        'code_update':'0130',
        'status':'PASS' if not errors else 'FAIL',
        'errors': errors,
        'data_classic_family_counts': family_counts,
        'evidence_sha256': sha(evidence) if evidence.exists() else None,
        'dotnet_sdk_available_here': False,
        'codex_required_commands': [
            'dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj',
            'dotnet run --project Apps/Source/Reforged.Server -- --smoke-generated-data',
            'dotnet run --project Apps/Source/Reforged.Server -- --run-local'
        ]
    }
    out = ROOT/'Reports/CodeUpdates/CodeUpdate0130/server_classic_readiness_validation_0130.json'
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, indent=2))
    return 0 if not errors else 1

if __name__ == '__main__':
    raise SystemExit(main())
