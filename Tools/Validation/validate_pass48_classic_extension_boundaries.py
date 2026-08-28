#!/usr/bin/env python3
import json, pathlib
from collections import Counter

root = pathlib.Path(__file__).resolve().parents[2]
checks, failures = [], []

def req(condition, name):
    checks.append(name)
    if not condition:
        failures.append(name)

def text(path):
    return (root / path).read_text(encoding="utf-8")

def js(path):
    return json.loads(text(path))

evidence = js("Librarium/Parity/majormud_classic_extension_boundary_evidence_0319.json")
ledger = js("Librarium/Parity/majormud_parity_assertions_0319.json")
score = js("Reports/RuntimeVerification/majormud_parity_scorecard_0319.json")
authority = text("Apps/Source/Reforged.Server/RuntimeClassicExtensionBoundaryAuthority.cs")
services = text("Apps/Source/Reforged.Server/RuntimeLiveGameplayPart6.cs")
world = text("Apps/Source/Reforged.Server/RuntimeWorldSystemsBundle.cs")
program = text("Apps/Source/Reforged.Server/Program.cs")

req(evidence["pass"] == 48 and evidence["update"] == "0319", "evidence-pass-update")
req(evidence["runtime"]["boundaryCount"] == 9, "nine-boundaries")
req(evidence["nahr8480Exhaustion"]["result"] == "remains UNKNOWN", "nahr-remains-unknown")
req("No prose is inferred" in evidence["nahr8480Exhaustion"]["reason"], "nahr-no-inference")

by_id = {row["id"]: row for row in ledger["assertions"]}
counts = Counter(row["state"] for row in ledger["assertions"])
req(ledger["update"] == "0319", "ledger-update")
req(counts == Counter({"PASS": 97, "STRONG": 89, "UNKNOWN": 14}), "ledger-counts")
req(by_id["native.reforged-extension-boundaries"]["state"] == "STRONG", "extension-strong")
req(by_id["npc.native-combat-message-8480"]["state"] == "UNKNOWN", "nahr-unknown")

req((score["PassCount"], score["StrongCount"], score["UnknownCount"]) == (97, 89, 14), "score-counts")
req(score["StrictPercent"] == 48.5 and score["EvidenceBackedPercent"] == 93.0, "score-percentages")

for token in ["trade", "repair", "RuntimeGlobalRandomService", "RuntimeClassicWorldgroupPrng", "CompatibilityUnresolved"]:
    req(token in authority, "authority-" + token.lower())
req("RuntimeClassicExtensionBoundaryAuthority.IsCommandAvailable" in services, "service-command-gate")
req("RuntimeClassicExtensionBoundaryAuthority.IsCommandAvailable" in world, "world-service-gate")
req("--smoke-pass48-classic-extension-boundaries" in program, "smoke-cli")

report = {
    "schema": "majormud.pass48-classic-extension-boundary-validation.v1",
    "update": "0319",
    "status": "PASS" if not failures else "FAIL",
    "checks": len(checks),
    "failures": failures,
}
print(json.dumps(report, indent=2))
raise SystemExit(0 if not failures else 1)
