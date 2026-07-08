#!/usr/bin/env python3
import json, sys
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
LIB = ROOT / "Librarium"
required = [
    LIB / "Intelligence" / "intelligence_layer_model_0009.json",
    LIB / "Services" / "knowledge_services_0009.json",
    LIB / "Services" / "service_api_contract_0009.json",
    LIB / "Queries" / "natural_language_query_templates_0009.json",
    LIB / "Tools" / "query_knowledge_services.py",
]
missing = [str(p) for p in required if not p.exists()]
status = "PASS" if not missing else "FAIL"
services = []
if not missing:
    services = json.loads((LIB / "Services" / "knowledge_services_0009.json").read_text()).get("services", [])
report = {
    "milestone": "Librarium-0009",
    "status": status,
    "missing": missing,
    "serviceCount": len(services),
    "serviceIds": [s.get("serviceId") for s in services],
}
print(json.dumps(report, indent=2))
(LIB / "Reports" / "librarium_0009_validation_report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
raise SystemExit(0 if status == "PASS" else 1)
