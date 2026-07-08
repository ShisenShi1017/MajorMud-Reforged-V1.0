from Runtime.data_runtime import DataRuntime

CORE_TABLES = ["rooms", "items", "monsters", "spells", "shops", "loot_tables"]


def validate_reference_integrity(repo_root):
    runtime = DataRuntime.from_repo_root(repo_root).load()
    passed = failed = unknown = 0
    notes = []
    for table in CORE_TABLES:
        records = runtime.get_records(table)
        if not records:
            unknown += 1
            notes.append(f"No records exposed for {table}; schema may need adapter.")
            continue
        id_field = runtime.find_id_field(records)
        if not id_field:
            unknown += 1
            notes.append(f"No obvious id field found for {table}; schema adapter required before deep reference checks.")
            continue
        seen = set()
        duplicates = 0
        missing = 0
        for row in records:
            value = row.get(id_field)
            if value in (None, ""):
                missing += 1
            elif value in seen:
                duplicates += 1
            else:
                seen.add(value)
        if missing or duplicates:
            failed += missing + duplicates
            notes.append(f"{table}: {missing} missing ids and {duplicates} duplicate ids using field {id_field}.")
        else:
            passed += 1
            notes.append(f"{table}: {len(records)} records have unique ids using field {id_field}.")
    return {"status": "PASS" if failed == 0 else "FAIL", "passed": passed, "failed": failed, "unknown": unknown, "notes": notes}
