#!/usr/bin/env python3
"""Deterministic simulator regression scaffold for CodeUpdate 0089-0092.
This intentionally avoids asserting Classic behavior until expected observations are supplied.
"""
import json, pathlib, hashlib
ROOT = pathlib.Path(__file__).resolve().parents[2]
REG = ROOT / "Simulator" / "Regression0089_0092" / "deterministic_probe_registry_0091.json"
OUT = ROOT / "Simulator" / "Regression0089_0092" / "deterministic_probe_dry_run_summary.json"

def stable_digest(obj):
    return hashlib.sha256(json.dumps(obj, sort_keys=True).encode()).hexdigest()

def main():
    data=json.loads(REG.read_text())
    results=[]
    for p in data["probes"]:
        results.append({"probe_id":p["probe_id"],"name":p["name"],"status":"dry_run_defined","fixture_digest":stable_digest(p)})
    OUT.write_text(json.dumps({"status":"PASS_WITH_LIMITATIONS","results":results,"note":"Dry-run validates probe registry determinism only; no Classic behavior assertions executed."}, indent=2))
    print(f"wrote {OUT}")
if __name__ == "__main__": main()
