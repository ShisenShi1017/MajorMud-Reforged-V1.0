#!/usr/bin/env python3
import json, pathlib
root=pathlib.Path(__file__).resolve().parents[2]
d=json.loads((root/"Librarium/Evidence/worldgroup_semantic_exhaustion_0037.json").read_text())
assert d["status"] == "semantically_exhausted_with_explicit_external_artifact_boundary"
assert d["compatibility"]["majormud_used_symbols"] == 156
assert d["compatibility"]["matched_exports"] == 156
assert d["compatibility"]["unmatched_exports"] == 0
assert d["room_exit_closure"]["distinct_latches"] == 7
assert d["room_exit_closure"]["exit_rows"] == 12
assert all(d["exit_criteria"].values())
assert d["external_evidence_boundary"]["closure_class"] == "conclusively_bounded_external_artifact_dependency"
assert len(d["correction_audit"]) == 4
print("Worldgroup Update0037 final compatibility and semantic exhaustion validated")
