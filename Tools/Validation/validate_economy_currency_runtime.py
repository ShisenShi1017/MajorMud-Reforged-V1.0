#!/usr/bin/env python3
from __future__ import annotations
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
RUNTIME = ROOT / "Apps/Source/Reforged.Server/RuntimeEconomyCurrency.cs"
CATALOG = ROOT / "Librarium/Runtime/Economy/currency_runtime_catalog_0150.json"
EVIDENCE = ROOT / "Evidence/Economy/economy_currency_runtime_evidence_0150.json"
MANIFEST = ROOT / "Manifests/economy_semantic_exhaustion_cumulative_manifest_0150.json"

errors: list[str] = []
for path in (RUNTIME, CATALOG, EVIDENCE, MANIFEST):
    if not path.is_file():
        errors.append(f"missing:{path.relative_to(ROOT)}")

for path in (CATALOG, EVIDENCE, MANIFEST):
    if path.is_file():
        try:
            json.loads(path.read_text(encoding="utf-8"))
        except Exception as exc:
            errors.append(f"json:{path.relative_to(ROOT)}:{exc}")

if RUNTIME.is_file():
    text = RUNTIME.read_text(encoding="utf-8")
    required = [
        "checked(quantity.Quantity * denomination.BaseUnits)",
        "currency-account-limit-exceeded",
        "RuntimeCurrencyTransferEngine",
        "source.RestoreUnsafe(sourceBefore)",
        "destination.RestoreUnsafe(destinationBefore)",
        "RuntimeCurrencyPersistence",
    ]
    for token in required:
        if token not in text:
            errors.append(f"runtime-token-missing:{token}")
    if len(re.findall(r"public sealed record RuntimeCurrency", text)) < 4:
        errors.append("runtime-model-count-too-low")

if MANIFEST.is_file():
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    for rel in manifest.get("files", []):
        if not (ROOT / rel).is_file():
            errors.append(f"manifest-file-missing:{rel}")

if errors:
    print(json.dumps({"valid": False, "errors": errors}, indent=2))
    sys.exit(1)
print(json.dumps({"valid": True, "checks": ["json", "runtime-contract", "rollback", "persistence", "manifest"]}, indent=2))
