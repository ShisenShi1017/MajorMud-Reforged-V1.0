#!/usr/bin/env python3
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / "Librarium" / "Evidence" / "worldgroup_unavailable_evidence_boundary_0039.json"
REGISTRY = ROOT / "Apps" / "Source" / "Reforged.Server" / "WorldgroupUnavailableEvidenceBoundaryRegistry.cs"
REPORT = ROOT / "Librarium" / "Reports" / "worldgroup_unavailable_evidence_boundary_0039.md"


def main() -> int:
    data = json.loads(EVIDENCE.read_text(encoding="utf-8"))
    assert data["update"] == "WorldgroupNativeClosureUpdate0039"
    assert data["status"] == "conclusive_external_evidence_boundary"
    policy = data["closure_policy"]
    assert policy["runtime_behavior_changed"] is False
    assert policy["compatibility_blocked"] is False
    assert policy["semantic_exhaustion_blocked"] is False
    assert policy["speculative_parser_activated"] is False
    assert policy["reopen_only_when_new_primary_evidence_exists"] is True
    assert REGISTRY.exists()
    assert REPORT.exists()
    registry_text = REGISTRY.read_text(encoding="utf-8")
    assert 'SpeculativeParserAllowed = false' in registry_text
    assert 'BlocksRuntimeImplementation = false' in registry_text
    print("Worldgroup unavailable-evidence boundary Update0039 validation passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
