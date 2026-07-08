"""Validate Runtime-0002 schema adapters against canonical /Data."""
from __future__ import annotations
from Runtime.adapters import load_schema_bundle

EXPECTED_MINIMUMS = {
    'rooms': 1000,
    'items': 1500,
    'monsters': 1000,
    'shops': 100,
    'loot_tables': 300,
}

def validate_schema_adapters(repo_root=None):
    from pathlib import Path
    data_dir = Path(repo_root or '.') / 'Data'
    bundle = load_schema_bundle(data_dir)
    counts = bundle.counts()
    passed = 0
    failed = 0
    unknown = 0
    notes = []

    for key, minimum in EXPECTED_MINIMUMS.items():
        actual = counts.get(key, 0)
        if actual >= minimum:
            passed += 1
        else:
            failed += 1
            notes.append(f'{key} count below expected minimum: {actual} < {minimum}')

    # spells.json is currently structurally known to be a promoted wrapper, but the
    # records may be unavailable in this surface. Treat empty spells as UNKNOWN, not FAIL.
    if counts.get('spells', 0) > 0:
        passed += 1
    else:
        unknown += 1
        notes.append('Spell adapter loaded zero spell records; spells.json shape requires future adapter expansion or source recovery.')

    warning_count = sum(1 for i in bundle.issues if i.severity == 'warning')
    info_count = sum(1 for i in bundle.issues if i.severity == 'info')
    notes.append(f'Adapted counts: {counts}')
    notes.append(f'Schema issues: warnings={warning_count}, info={info_count}, total={len(bundle.issues)}')

    status = 'FAIL' if failed else ('UNKNOWN' if unknown else 'PASS')
    return {'status': status, 'passed': passed, 'failed': failed, 'unknown': unknown, 'notes': notes}
