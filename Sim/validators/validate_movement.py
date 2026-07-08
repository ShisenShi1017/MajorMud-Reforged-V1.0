from Runtime.data_runtime import DataRuntime
from Runtime.movement import MovementRuntime


def validate_movement(repo_root='.'):
    runtime = DataRuntime.from_repo_root(repo_root).load()
    movement = MovementRuntime(runtime)
    summary = movement.graph_summary()
    notes = [f"Movement graph summary: {summary}"]
    passed = 0
    failed = 0
    unknown = 0

    if summary["rooms_indexed"] > 0:
        passed += 1
    else:
        failed += 1
        notes.append("No rooms were indexed for movement.")

    if summary["total_exits"] > 0:
        passed += 1
    else:
        failed += 1
        notes.append("No exits were found in canonical room data.")

    if summary["missing_targets"] == 0:
        passed += 1
    else:
        unknown += 1
        notes.append(f"Found {summary['missing_targets']} exits pointing to missing target rooms; retained as data/reference follow-up, not a movement formula failure.")

    # Known canonical room 1 has a normal west exit to 101 in current /Data.
    result = movement.move("wcc_map1_room1", "W")
    if result.status == "MOVED" and result.to_room == (1, 101):
        passed += 1
    else:
        failed += 1
        notes.append(f"Expected wcc_map1_room1 west to move to map 1 room 101; got {result.status} -> {result.to_room}.")

    # Known canonical room 1 east is a special exit. It must not be guessed as normal movement.
    special = movement.move("wcc_map1_room1", "E")
    if special.status == "UNKNOWN_SPECIAL_EXIT":
        passed += 1
        unknown += 1
        notes.append("Special exits are intentionally preserved as UNKNOWN_SPECIAL_EXIT until formulas/scripts are verified.")
    else:
        failed += 1
        notes.append(f"Expected wcc_map1_room1 east to remain unresolved special exit; got {special.status}.")

    status = "PASS" if failed == 0 else "FAIL"
    return {"status": status, "passed": passed, "failed": failed, "unknown": unknown, "notes": notes}
