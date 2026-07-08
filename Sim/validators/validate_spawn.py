from Runtime.data_runtime import DataRuntime
from Runtime.spawn import SpawnRuntime


def validate_spawn(repo_root='.'):
    runtime = DataRuntime.from_repo_root(repo_root).load()
    spawn = SpawnRuntime(runtime)
    summary = spawn.summary()
    notes = [f"Spawn runtime summary: {summary}"]
    passed = 0
    failed = 0
    unknown = 0

    if summary["rooms_indexed"] > 0:
        passed += 1
    else:
        failed += 1
        notes.append("No rooms were indexed for spawn runtime.")

    if summary["monsters_indexed"] > 0:
        passed += 1
    else:
        failed += 1
        notes.append("No monsters were indexed for spawn runtime.")

    if summary["spawn_records"] > 0:
        passed += 1
    else:
        failed += 1
        notes.append("No room spawn records were found in canonical room data.")

    if summary["missing_monster_refs"] == 0:
        passed += 1
    else:
        unknown += 1
        notes.append(f"Found {summary['missing_monster_refs']} spawn monster references not matched to monster legacyNumber; retained as data/reference follow-up.")

    events = spawn.schedule_initial_spawns(current_minute=100)
    if len(events) == summary["spawn_records"]:
        passed += 1
    else:
        failed += 1
        notes.append(f"Expected one scheduled event per spawn record; got {len(events)} events for {summary['spawn_records']} records.")

    room2 = spawn.records_for_room("wcc_map1_room2")
    if room2 and room2[0].monster_type == 6 and room2[0].respawn_minutes == 5:
        passed += 1
    else:
        failed += 1
        notes.append(f"Expected wcc_map1_room2 spawn monsterType 6 with 5-minute respawn; got {room2}.")

    if summary["boss_spawn_records"] > 0:
        unknown += 1
        notes.append("Boss spawn records are indexed but boss semantics remain UNKNOWN until verified.")

    if summary["linked_spawn_records"] > 0:
        unknown += 1
        notes.append("Linked spawn records are indexed but linked behavior remains UNKNOWN until verified.")

    status = "PASS" if failed == 0 else "FAIL"
    return {"status": status, "passed": passed, "failed": failed, "unknown": unknown, "notes": notes}
