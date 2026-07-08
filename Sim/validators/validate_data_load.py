from Runtime.data_runtime import DataRuntime, CANONICAL_FILES

REQUIRED_TABLES = ["rooms", "items", "monsters", "spells", "shops", "classes", "races", "skills"]


def validate_data_load(repo_root):
    runtime = DataRuntime.from_repo_root(repo_root).load()
    passed = failed = unknown = 0
    notes = []
    fail_issues = [i for i in runtime.issues if i.severity == "FAIL"]
    if fail_issues:
        failed += len(fail_issues)
        notes.extend([i.message for i in fail_issues])
    for table in REQUIRED_TABLES:
        count = runtime.count(table)
        if count > 0:
            passed += 1
            notes.append(f"Loaded {table}: {count}")
        else:
            failed += 1
            notes.append(f"Required table empty or missing: {table}")
    missing_optional = [name for name in CANONICAL_FILES if name not in runtime.tables]
    unknown += len(missing_optional)
    if missing_optional:
        notes.append("Optional/secondary canonical files missing: " + ", ".join(missing_optional))
    return {"status": "PASS" if failed == 0 else "FAIL", "passed": passed, "failed": failed, "unknown": unknown, "notes": notes}
