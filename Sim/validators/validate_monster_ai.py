from Runtime.data_runtime import DataRuntime
from Runtime.monster_ai import MonsterAIRuntime


def validate_monster_ai(repo_root='.'):
    runtime = DataRuntime.from_repo_root(repo_root).load()
    ai = MonsterAIRuntime(runtime)
    summary = ai.summary()
    notes = [f"Monster AI runtime summary: {summary}"]
    passed = 0
    failed = 0
    unknown = 0

    if summary["monsters_indexed"] > 0:
        passed += 1
    else:
        failed += 1
        notes.append("No monsters were indexed for monster AI runtime.")

    if summary["ai_profiles"] == summary["monsters_indexed"]:
        passed += 1
    else:
        failed += 1
        notes.append(f"Expected one AI profile per monster; got {summary['ai_profiles']} profiles for {summary['monsters_indexed']} monsters.")

    if summary["follow_candidates"] > 0:
        passed += 1
        unknown += 1
        notes.append("Follow candidates are indexed, but exact classic follow probability/timing remains UNKNOWN.")
    else:
        failed += 1
        notes.append("No follow candidates found; expected canonical behavior.follow values to exist.")

    acid = ai.get_profile(5)
    if acid and acid.name == "acid slime" and acid.follow_score == 15:
        passed += 1
    else:
        failed += 1
        notes.append(f"Expected legacy monster 5 acid slime with follow score 15; got {acid}.")

    follow_decision = ai.classify_follow(5)
    if follow_decision.action == "FOLLOW_CANDIDATE" and follow_decision.status == "UNKNOWN":
        passed += 1
    else:
        failed += 1
        notes.append(f"Expected acid slime to be a follow candidate with UNKNOWN semantics; got {follow_decision}.")

    assist_decision = ai.classify_assist(5, 1023)
    if assist_decision.action in {"ASSIST_CANDIDATE", "NO_ASSIST_BY_GROUP"}:
        passed += 1
        if assist_decision.status == "UNKNOWN":
            unknown += 1
    else:
        failed += 1
        notes.append(f"Unexpected assist decision result: {assist_decision}.")

    pack_decision = ai.classify_pack(5)
    if pack_decision.action in {"PACK_CANDIDATE", "SOLO_GROUP"}:
        passed += 1
        if pack_decision.status == "UNKNOWN":
            unknown += 1
    else:
        failed += 1
        notes.append(f"Unexpected pack decision result: {pack_decision}.")

    aggro_decision = ai.classify_aggro(5)
    if aggro_decision.action == "AGGRO_UNRESOLVED" and aggro_decision.status == "UNKNOWN":
        passed += 1
        unknown += 1
        notes.append("Aggro surface exists, but exact aggro semantics remain UNKNOWN until runtime evidence verifies them.")
    else:
        failed += 1
        notes.append(f"Unexpected aggro decision result: {aggro_decision}.")

    if summary["grouped_profiles"] > 0 and summary["group_ids"] > 0:
        passed += 1
    else:
        unknown += 1
        notes.append("No group/pack indexing surface found; this may require data follow-up.")

    status = "PASS" if failed == 0 else "FAIL"
    return {"status": status, "passed": passed, "failed": failed, "unknown": unknown, "notes": notes}
